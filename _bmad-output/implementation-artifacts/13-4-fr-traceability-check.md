# Story 13.4: FR Traceability Check

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a documentation maintainer,
I want to verify that every functional requirement has at least one corresponding documentation page,
so that I can identify coverage gaps before they reach users.

## Acceptance Criteria

1. A traceability mapping document exists at `docs/fr-traceability.md` that lists each FR number (FR1–FR63) alongside the documentation page(s) that address it
2. Any FR without a corresponding documentation page is flagged as a gap with status `GAP` and a note indicating which epic/phase will deliver it
3. The check can be run manually by reading the document — automated CI enforcement is not required for MVP

## Exact File Manifest

The dev agent must create or modify exactly these files:

| Action | File                      | Purpose                                      |
| ------ | ------------------------- | -------------------------------------------- |
| CREATE | `docs/fr-traceability.md` | FR-to-documentation-page traceability matrix |

No other source/configuration files. Do NOT modify CI workflows, solution files, or any files in `src/`, `tests/`, `samples/`, or `deploy/`. BMAD workflow tracking artifacts under `_bmad-output/implementation-artifacts/` may be updated automatically by workflow execution.

## Tasks / Subtasks

- [x] Task 1: Create `docs/fr-traceability.md` (AC: #1, #2, #3)
    - [x] 1.1 Create the markdown document with page template header (title, description, last-reviewed date)
    - [x] 1.2 Build the complete FR traceability table with all 63 FRs
    - [x] 1.3 For each FR, identify the documentation page(s) that address it by scanning existing `docs/`, root `.md` files, `.github/` templates, CI workflows, and `samples/` assets
    - [x] 1.4 Flag FRs without documentation pages as `GAP` with the epic/phase that will deliver them
    - [x] 1.5 Add a summary section with coverage statistics (covered/partial/gap counts)
    - [x] 1.6 Add a "How to Use This Document" section explaining the traceability check process
    - [x] 1.7 Verify the document passes markdownlint (consistent with `.markdownlint-cli2.jsonc`)

## Dev Notes

### CRITICAL: Purpose and Scope

This document is a **manual traceability matrix** — a markdown table that maps each of the 63 functional requirements (from `_bmad-output/planning-artifacts/prd-documentation.md`) to the documentation page(s) that address them. It is NOT a script, NOT CI-enforced, and NOT auto-generated.

**FR62 states:** "A maintainer can verify that every functional requirement has at least one corresponding documentation page through a traceability check."

The acceptance criteria explicitly says: "the check can be run manually (a markdown table or script output) — automated CI enforcement is not required for MVP."

### CRITICAL: Document Structure

The document MUST follow the page template pattern established by architecture decision D1. Use the standard page template from `docs/page-template.md` as a structural guide (title, description, prerequisites, content, next steps).

**Document sections:**

1. **Title & Description** — "FR Traceability Matrix" with a one-line description
2. **How to Use This Document** — Brief instructions for maintainers on how to perform the traceability check
3. **Summary** — Coverage statistics: total FRs, covered count, partial count, gap count, coverage percentage
4. **Traceability Matrix Table** — The complete mapping table with columns: FR #, Description, Status, Documentation Page(s), Notes
5. **Gap Analysis** — Summary of gaps grouped by epic/phase for prioritization

### CRITICAL: Status Values

Each FR gets one of three status values:

| Status    | Meaning                                               |
| --------- | ----------------------------------------------------- |
| `COVERED` | One or more documentation pages fully address this FR |
| `PARTIAL` | Some content exists but does not fully satisfy the FR |
| `GAP`     | No documentation page addresses this FR yet           |

### CRITICAL: Complete FR-to-Page Mapping

The dev agent MUST scan the entire documentation surface to build the mapping. The documentation surface includes:

**Root-level files:**

- `README.md` — project overview, programming model, architecture diagram, comparison summary
- `CHANGELOG.md` — release history and breaking changes
- `CONTRIBUTING.md` — contribution workflow, PR process, local validation
- `CODE_OF_CONDUCT.md` — community standards

**docs/ directory:**

- `docs/getting-started/prerequisites.md` — prerequisites and local dev setup
- `docs/getting-started/quickstart.md` — 10-minute quickstart guide
- `docs/getting-started/first-domain-service.md` — domain service tutorial
- `docs/concepts/architecture-overview.md` — architecture topology with Mermaid
- `docs/concepts/choose-the-right-tool.md` — decision aid, comparison, DAPR trade-offs, "when NOT to use"
- `docs/concepts/command-lifecycle.md` — end-to-end command lifecycle
- `docs/concepts/event-envelope.md` — event envelope metadata structure
- `docs/concepts/identity-scheme.md` — identity scheme and actor/stream/topic mapping
- `docs/community/awesome-event-sourcing.md` — curated ecosystem page
- `docs/reference/command-api.md` — REST endpoint reference
- `docs/reference/nuget-packages.md` — NuGet package guide and dependency graph

**GitHub infrastructure:**

- `.github/ISSUE_TEMPLATE/01-bug-report.yml` — bug report template
- `.github/ISSUE_TEMPLATE/02-feature-request.yml` — feature request template
- `.github/ISSUE_TEMPLATE/03-docs-improvement.yml` — documentation improvement template
- `.github/PULL_REQUEST_TEMPLATE.md` — PR template and checklist
- `.github/DISCUSSION_TEMPLATE/ideas.yml` — ideas discussion template
- `.github/DISCUSSION_TEMPLATE/q-a.yml` — Q&A discussion template

**CI workflows:**

- `.github/workflows/docs-validation.yml` — markdown linting, link checking, sample build/test
- `.github/workflows/ci.yml` — main CI pipeline

**Samples:**

- `samples/dapr-components/redis/` — Redis DAPR component YAML (backend swap demo)
- `samples/dapr-components/postgresql/` — PostgreSQL DAPR component YAML (backend swap demo)
- `samples/Hexalith.EventStore.Sample/` — Counter domain sample
- `samples/Hexalith.EventStore.Sample.Tests/` — Quickstart smoke tests

### CRITICAL: Expected FR Mapping (Reference for Dev Agent)

This is the expected mapping based on exhaustive analysis. The dev agent MUST verify each mapping by reading the actual file content, not just trusting this list.

**Phase 1a — Foundation (Epics 8-10, mapped to doc Epics 1-4):**

| FR   | Status  | Page(s)                                                                                                                 | Notes                                                                                   |
| ---- | ------- | ----------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| FR1  | COVERED | `README.md`                                                                                                             | 30-second understanding via hero section, programming model, architecture diagram       |
| FR2  | COVERED | `README.md`                                                                                                             | Pure function contract `(Command, CurrentState?) → List<DomainEvent>` in first scroll   |
| FR3  | COVERED | `docs/concepts/choose-the-right-tool.md`                                                                                | Structured decision guide at end of page                                                |
| FR4  | COVERED | `docs/concepts/choose-the-right-tool.md`                                                                                | Comparison table vs Marten, EventStoreDB, custom                                        |
| FR5  | COVERED | `README.md`                                                                                                             | Animated GIF demo (`docs/assets/quickstart-demo.gif`)                                   |
| FR6  | COVERED | `docs/getting-started/prerequisites.md`                                                                                 | Prerequisites page                                                                      |
| FR7  | COVERED | `docs/getting-started/quickstart.md`                                                                                    | 10-minute quickstart guide                                                              |
| FR8  | COVERED | `docs/getting-started/first-domain-service.md`                                                                          | Step-by-step domain service tutorial                                                    |
| FR9  | COVERED | `samples/dapr-components/redis/`, `samples/dapr-components/postgresql/`, `docs/getting-started/first-domain-service.md` | Backend swap demo with YAML variants                                                    |
| FR10 | COVERED | `docs/getting-started/quickstart.md`                                                                                    | Send command, observe event in quickstart                                               |
| FR11 | COVERED | `docs/concepts/architecture-overview.md`                                                                                | Architecture topology with Mermaid diagram                                              |
| FR12 | COVERED | `docs/concepts/event-envelope.md`                                                                                       | Event envelope metadata structure                                                       |
| FR13 | COVERED | `docs/concepts/identity-scheme.md`                                                                                      | Identity scheme and mapping                                                             |
| FR14 | COVERED | `docs/concepts/command-lifecycle.md`                                                                                    | End-to-end command lifecycle trace                                                      |
| FR15 | COVERED | `docs/concepts/choose-the-right-tool.md`                                                                                | DAPR trade-offs integrated into comparison page                                         |
| FR16 | COVERED | `docs/concepts/choose-the-right-tool.md`                                                                                | "When NOT to Use Hexalith" section                                                      |
| FR17 | COVERED | `docs/reference/command-api.md`                                                                                         | REST endpoint reference with request/response examples                                  |
| FR18 | COVERED | `docs/reference/nuget-packages.md`                                                                                      | NuGet package guide per use case                                                        |
| FR19 | GAP     | —                                                                                                                       | Auto-generated API docs (Epic 15, Phase 2)                                              |
| FR20 | COVERED | `docs/reference/nuget-packages.md`                                                                                      | NuGet dependency graph                                                                  |
| FR21 | GAP     | —                                                                                                                       | Configuration reference (Epic 15, Phase 2)                                              |
| FR22 | GAP     | —                                                                                                                       | Docker Compose deployment guide (Epic 14, Phase 2)                                      |
| FR23 | GAP     | —                                                                                                                       | Kubernetes deployment guide (Epic 14, Phase 2)                                          |
| FR24 | GAP     | —                                                                                                                       | Azure Container Apps deployment guide (Epic 14, Phase 2)                                |
| FR25 | GAP     | —                                                                                                                       | DAPR component configuration reference (Epic 14, Phase 2)                               |
| FR26 | GAP     | —                                                                                                                       | Health/readiness endpoint documentation (Epic 14, Phase 2)                              |
| FR27 | GAP     | —                                                                                                                       | Security model documentation (Epic 14, Phase 2)                                         |
| FR28 | COVERED | `CONTRIBUTING.md`                                                                                                       | Contribution workflow with fork, branch, PR steps                                       |
| FR29 | PARTIAL | `.github/ISSUE_TEMPLATE/config.yml`                                                                                     | Issue templates exist but no "good first issue" label strategy documented               |
| FR30 | COVERED | `.github/ISSUE_TEMPLATE/01-bug-report.yml`, `02-feature-request.yml`, `03-docs-improvement.yml`                         | Three structured issue templates                                                        |
| FR31 | COVERED | `.github/PULL_REQUEST_TEMPLATE.md`                                                                                      | PR template with checklist                                                              |
| FR32 | COVERED | `.github/DISCUSSION_TEMPLATE/ideas.yml`, `q-a.yml`                                                                      | GitHub Discussions with Ideas and Q&A categories                                        |
| FR33 | GAP     | —                                                                                                                       | Public product roadmap (Epic 15, Phase 2)                                               |
| FR34 | COVERED | `.github/workflows/docs-validation.yml`                                                                                 | CI validates code examples compile via sample build/test                                |
| FR35 | COVERED | `.github/workflows/docs-validation.yml`                                                                                 | CI detects broken links via lychee                                                      |
| FR36 | COVERED | `.github/workflows/docs-validation.yml`                                                                                 | CI enforces markdown formatting via markdownlint-cli2                                   |
| FR37 | PARTIAL | `.github/workflows/docs-validation.yml`                                                                                 | Stale content detection CI configured (Epic 11, story 11-4 done) but limited scope      |
| FR38 | COVERED | `CONTRIBUTING.md`, `.github/PULL_REQUEST_TEMPLATE.md`                                                                   | PR review process documented                                                            |
| FR39 | COVERED | `README.md`                                                                                                             | GitHub search keywords in title, description, topics                                    |
| FR40 | COVERED | `docs/**/*.md`                                                                                                          | Descriptive URLs via folder structure, structured headings                              |
| FR41 | COVERED | `docs/community/awesome-event-sourcing.md`                                                                              | Curated ecosystem page                                                                  |
| FR42 | COVERED | All `docs/**/*.md` pages                                                                                                | Cross-linking via "Next Steps" sections on every page                                   |
| FR43 | COVERED | All `docs/**/*.md` pages                                                                                                | Self-contained pages with "What You'll Learn" + context                                 |
| FR44 | COVERED | `README.md`, navigation in docs pages                                                                                   | Progressive complexity path from README → quickstart → concepts → reference             |
| FR45 | COVERED | `README.md`                                                                                                             | Architecture link in README as parallel entry point                                     |
| FR46 | PARTIAL | `docs/**/*.md`                                                                                                          | Position hints in "Prerequisites" and "Next Steps" sections; no global nav              |
| FR47 | GAP     | —                                                                                                                       | Quickstart troubleshooting (Epic 14, Phase 2)                                           |
| FR48 | GAP     | —                                                                                                                       | DAPR integration troubleshooting (Epic 14, Phase 2)                                     |
| FR49 | GAP     | —                                                                                                                       | Deployment failure troubleshooting (Epic 14, Phase 2)                                   |
| FR50 | COVERED | `CHANGELOG.md`                                                                                                          | Changelog with breaking changes and migration steps                                     |
| FR51 | GAP     | —                                                                                                                       | Event versioning and schema evolution (Epic 15, Phase 2)                                |
| FR52 | GAP     | —                                                                                                                       | Upgrade path documentation (Epic 15, Phase 2)                                           |
| FR53 | COVERED | `docs/getting-started/prerequisites.md`                                                                                 | Local dev environment setup                                                             |
| FR54 | COVERED | `README.md`                                                                                                             | Version reference linking to release tag                                                |
| FR55 | GAP     | —                                                                                                                       | Disaster recovery procedure (Epic 14, Phase 2)                                          |
| FR56 | GAP     | —                                                                                                                       | Deployment progression guide (Epic 14, Phase 2)                                         |
| FR57 | GAP     | —                                                                                                                       | DAPR runtime setup per environment (Epic 14, Phase 2)                                   |
| FR58 | GAP     | —                                                                                                                       | Infrastructure differences documentation (Epic 14, Phase 2)                             |
| FR59 | GAP     | —                                                                                                                       | Quickstart-to-deployment transition (Epic 14, Phase 2)                                  |
| FR60 | GAP     | —                                                                                                                       | Event data storage per backend (Epic 14, Phase 2)                                       |
| FR61 | PARTIAL | `CONTRIBUTING.md`                                                                                                       | Local validation referenced; scripts in story 13-3 (ready-for-dev, not yet implemented) |
| FR62 | —       | `docs/fr-traceability.md` (this story)                                                                                  | Self-referential: this document IS the traceability check                               |
| FR63 | GAP     | —                                                                                                                       | Resource sizing guidance (Epic 14, Phase 2)                                             |

**Coverage summary (excluding FR62):**

- COVERED: 39 FRs
- PARTIAL: 4 FRs (FR29, FR37, FR46, FR61)
- GAP: 19 FRs (all Phase 2 / Epic 14-15 scope)
- Coverage: 62/62 mapped = 100% traceability (39 covered + 4 partial + 19 gaps identified)

### CRITICAL: Markdown Formatting

The document MUST pass markdownlint as configured in `.markdownlint-cli2.jsonc`. Key rules:

- No trailing spaces
- Consistent heading hierarchy (H1 → H2 → H3, no skips)
- No inline HTML (use markdown only)
- Fenced code blocks with language tags
- Single H1 at top of document
- Blank line before and after headings, lists, code blocks

### CRITICAL: Page Template Compliance

Follow the established page template pattern (from `docs/page-template.md` and architecture decision D1):

- Title as H1
- One-line description
- "What You'll Learn" or "How to Use This Document" section
- Main content
- Cross-references to source documents

### Exact Document Content

The dev agent should create the document with approximately the following structure. The exact FR descriptions MUST match the PRD (`_bmad-output/planning-artifacts/prd-documentation.md`) — do NOT paraphrase or abbreviate them.

```markdown
# FR Traceability Matrix

Every functional requirement (FR1–FR63) from the [product requirements document](...) mapped to the documentation page(s) that address it. Use this document to identify coverage gaps and prioritize documentation work.

## How to Use This Document

1. Scan the **Status** column for `GAP` or `PARTIAL` entries
2. Check the **Notes** column for which epic/phase will deliver the missing content
3. After creating or updating documentation, update this table
4. Review this document before each documentation milestone to confirm coverage

## Summary

| Metric           | Count                          |
| ---------------- | ------------------------------ |
| Total FRs        | 63                             |
| Covered          | 39                             |
| Partial          | 4                              |
| Gap              | 19                             |
| Self-referential | 1 (FR62)                       |
| Phase 1 coverage | 69% (43/62 covered or partial) |

[... full traceability table ...]

## Gap Analysis by Phase

### Phase 2 — Epic 14: Deployment & Operations (13 gaps)

[list]

### Phase 2 — Epic 15: Configuration, Versioning & Lifecycle (5 gaps)

[list]

### Phase 1b — Pending Stories (1 partial)

[list]
```

### Project Structure Notes

- **Location:** `docs/fr-traceability.md` — lives in `docs/` alongside other documentation pages
- **Not a separate directory:** Single file, no subdirectory needed
- **Subject to docs-validation CI:** This file will be linted by markdownlint and link-checked by lychee as part of the existing `docs-validation.yml` pipeline
- **Not referenced from README:** This is an internal maintainer document, not user-facing navigation

### DO NOT

- Do NOT create a script — the AC explicitly allows "a markdown table or script output" and a static markdown document is the simplest valid approach
- Do NOT add CI enforcement — the AC explicitly says "automated CI enforcement is not required for MVP"
- Do NOT modify `.github/workflows/docs-validation.yml` or any CI workflow
- Do NOT modify `README.md`, `CONTRIBUTING.md`, or any existing documentation page
- Do NOT add the document to a navigation index or table of contents
- Do NOT paraphrase FR descriptions — use the exact text from the PRD/epics
- Do NOT create multiple files — the entire traceability matrix is a single markdown document
- Do NOT modify any files in `src/`, `tests/`, `samples/`, or `deploy/`
- Do NOT modify `.markdownlint-cli2.jsonc`, `.markdownlintignore`, `lychee.toml`, or `.lycheeignore`

### References

- [Source: _bmad-output/planning-artifacts/epics.md, Epic 6 / Story 6.4]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md, FR62 — FR traceability check]
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md, D1 — content folder structure]
- [Source: _bmad-output/planning-artifacts/epics.md, FR Coverage Map — FR-to-epic mapping]
- [Source: docs/page-template.md — standard page template for structure reference]
- [Source: .markdownlint-cli2.jsonc — linting rules the document must pass]
- [Source: _bmad-output/implementation-artifacts/13-3-local-validation-script.md — previous story patterns]

### Previous Story & Git Intelligence

- Story 13-3 (ready-for-dev) creates validation scripts in `scripts/` — confirms pattern of tooling files separate from `docs/` content
- Story 13-2 (review) created YAML documentation assets in `samples/dapr-components/` — pattern of educational assets outside `src/`
- Story 13-1 (done, commit `fba3ddb`) created quickstart smoke tests — FR34 coverage via CI sample build/test validation
- Story 12-5 (done) delivered "DAPR trade-offs and FAQ intro" — content integrated into `docs/concepts/choose-the-right-tool.md` rather than a separate FAQ page, covering FR15
- Recent commits focus on Epic 13 sample integration and validation stories — this story completes the Epic 13 scope
- All existing docs pages follow the page template pattern with "What You'll Learn", content sections, and "Next Steps" — the traceability document should follow a similar maintainer-oriented variant
- All 13 existing docs pages plus 4 root-level markdown files form the current documentation surface to map against 63 FRs
- Phase 1a+1b FRs (Epics 8-13, mapped to doc Epics 1-6) are largely covered; Phase 2 FRs (Epics 14-15, mapped to doc Epics 7-8) are expected gaps

### Verification Criteria (for Code Reviewer)

1. **All 63 FRs listed:** The table contains exactly 63 rows (FR1–FR63), one per FR
2. **FR descriptions match PRD:** Each FR description matches the text in `_bmad-output/planning-artifacts/prd-documentation.md` (or `epics.md`)
3. **Status accuracy:** Spot-check 5+ FRs marked as COVERED by opening the linked page and verifying the content exists
4. **Gap accuracy:** Verify that GAP FRs correspond to Phase 2 epics (14, 15) or unimplemented stories
5. **Summary statistics match:** Covered + Partial + Gap + Self-referential = 63
6. **Markdownlint passes:** `npx markdownlint-cli2 docs/fr-traceability.md` exits 0
7. **No broken links:** All relative page links in the Documentation Page(s) column resolve to existing files
8. **Page template compliance:** Document has H1 title, description, "How to Use" section, and content sections

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- 2026-03-02: Senior review remediation — added missing `Last reviewed` field to `docs/fr-traceability.md` to satisfy Task 1.1 page-template header requirement.
- 2026-03-02: Re-validated documentation quality checks for `docs/fr-traceability.md` (markdownlint + lychee) and FR coverage completeness (FR1–FR63 present).

### Completion Notes List

- Created `docs/fr-traceability.md` with complete FR1–FR63 traceability matrix
- Verified all 63 FR descriptions match exact text from PRD (`_bmad-output/planning-artifacts/prd-documentation.md`)
- Corrected FR29 from PARTIAL to COVERED — `CONTRIBUTING.md` has "Good First Issues" section (line 157) documenting the `good first issue` label strategy
- Corrected FR61 from PARTIAL to COVERED — `CONTRIBUTING.md` has "Run Docs Validation Locally" section (line 94) and `scripts/validate-docs.sh`/`scripts/validate-docs.ps1` exist
- Final coverage: 39 COVERED, 2 PARTIAL (FR37, FR46), 21 GAP (all Phase 2 scope), 1 self-referential (FR62)
- Document follows page template pattern (back-link, H1 title, description, content sections, Next Steps)
- Added explicit `Last reviewed` metadata field to satisfy page template header requirements
- markdownlint passes with 0 errors
- All relative links verified as valid
- Noted concurrent workspace changes from other stories during review; story implementation scope remains limited to `docs/fr-traceability.md`

### Senior Developer Review (AI)

Review Date: 2026-03-02
Reviewer: Jerome (AI-assisted)
Outcome: Changes Requested → Fixed in-session

Findings addressed:

- **HIGH** — Task 1.1 required a page-template header including last-reviewed date, but the document initially lacked it

- Fix applied: Added `**Last reviewed:** 2026-03-02` to `docs/fr-traceability.md`

- **MEDIUM** — Review transparency gap in story record

- Fix applied: Updated Debug Log References and Completion Notes to document review remediation and concurrent workspace context

- **MEDIUM** — Validation evidence not explicitly captured in review remediation notes

- Fix applied: Recorded markdownlint/lychee and FR coverage re-validation in Dev Agent Record

### Change Log

- 2026-03-02: Created FR traceability matrix document (`docs/fr-traceability.md`) mapping all 63 functional requirements to documentation pages with gap analysis by phase
- 2026-03-02: Senior Developer Review (AI) remediation — added missing `Last reviewed` metadata and updated Dev Agent Record for audit transparency

### File List

| Action | File                      |
| ------ | ------------------------- |
| CREATE | `docs/fr-traceability.md` |
