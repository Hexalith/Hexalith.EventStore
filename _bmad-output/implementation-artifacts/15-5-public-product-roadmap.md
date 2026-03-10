# Story 15.5: Public Product Roadmap

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer evaluating Hexalith's future direction,
I want to view the public product roadmap,
so that I can understand what's planned and assess the project's trajectory.

## Acceptance Criteria

1. **Given** a developer navigates to `docs/community/roadmap.md` **When** they read the page **Then** the page shows planned features and milestones organized by timeframe or priority
2. **And** the page includes a link to GitHub Issues/Milestones for real-time tracking
3. **And** the page follows the standard page template: back-link `[← Back to Hexalith.EventStore](../../README.md)`, H1, summary paragraph, prerequisites/tip callout, content sections, Next Steps footer
4. **And** the README Community section links to the roadmap page
5. **And** `docs/fr-traceability.md` is updated: FR33 from `GAP` to `COVERED` referencing `docs/community/roadmap.md`
6. **And** markdownlint-cli2 passes with project config (`.markdownlint-cli2.jsonc`)

## Tasks / Subtasks

- [x] Task 1: Create `docs/community/roadmap.md` (AC: #1, #2, #3)
    - [x] 1.1 Write page following standard template: back-link `[← Back to Hexalith.EventStore](../../README.md)`, H1 "Product Roadmap", summary paragraph explaining the roadmap's purpose and how to participate
    - [x] 1.2 Add tip/prerequisite callout linking to GitHub Issues and Discussions for real-time tracking
    - [x] 1.3 Section: "Current Focus" — summarize what's actively being built (document based on current sprint status: Epic 15 lifecycle docs completion, Epic 17 actor-based authorization & query API)
    - [x] 1.4 Section: "Planned" — organized by priority or theme (e.g., remaining Phase 2 documentation: deployment guides from Epic 14; new feature development: Epic 17 auth & query; future possibilities)
    - [x] 1.5 Section: "Completed" — use human-readable feature names, NOT epic numbers (readers don't know what "Epic 4" means). Example: "Event distribution with CloudEvents 1.0 and dead-letter handling", "Multi-tenant security with data-path and topic isolation", "Fluent Client SDK with convention engine and auto-discovery". See "Current Project State" below for the full mapping.
    - [x] 1.6 **CRITICAL SECTION:** "How to Influence the Roadmap" — this is the "Contribute" funnel endpoint and the highest-value section on the page. Links to GitHub Discussions (Ideas category), Issue Tracker, Contributing guide. Frame it as an invitation, not an afterthought.
    - [x] 1.7 Section: "Versioning and Release Cadence" — brief note about MinVer/SemVer, link to CHANGELOG and upgrade-path
    - [x] 1.8 Next Steps footer: links to GitHub Discussions, Issue Tracker, CONTRIBUTING.md, awesome-event-sourcing.md
- [x] Task 2: Update README.md (AC: #4)
    - [x] 2.1 Add `[Product Roadmap](docs/community/roadmap.md) — planned features and project direction` to the Community section in README.md
- [x] Task 3: Update FR traceability (AC: #5)
    - [x] 3.1 Update `docs/fr-traceability.md` — set FR33 from `GAP` to `COVERED` referencing `docs/community/roadmap.md`
    - [x] 3.2 Update gap summary counts (decrement by 1 from current value — see cross-dependency warning in Dev Notes)
    - [x] 3.3 Update coverage percentage (recalculate from current values — see cross-dependency warning in Dev Notes)
    - [x] 3.4 Remove FR33 from the Epic 15 gap analysis table (4 gaps → 3 gaps in that section)
- [x] Task 4: Validate with markdownlint-cli2 (AC: #6)
    - [x] 4.1 Run `npx markdownlint-cli2 docs/community/roadmap.md` — 0 errors
    - [x] 4.2 Verify all internal links resolve to existing files
- [x] Task 5: Clean up `docs/community/.gitkeep` (housekeeping)
    - [x] 5.1 Remove `docs/community/.gitkeep` — no longer needed once `roadmap.md` and `awesome-event-sourcing.md` exist in the folder

## Dev Notes

### Architecture Context: Documentation Structure

The roadmap page lives in `docs/community/` alongside `awesome-event-sourcing.md`. This folder serves the "Contribute" stage of the developer funnel (Hook → Try → Build → Trust → Stay → Contribute).

**Standard page template** (consistent across all doc pages):

```markdown
[← Back to Hexalith.EventStore](../../README.md)

# Page Title

Summary paragraph.

> **Tip/Prerequisites:** callout block

## Content Sections

## Next Steps
```

### Content Strategy: Living Document, Not Detailed Spec

The roadmap page's **primary job is to signal active, thoughtful development** — it is a trust and credibility artifact for a pre-v1.0 project. Evaluators and contributors should leave the page confident the project has direction and momentum. Do not create a dry feature list — convey intentionality.

The roadmap page should:

- **Be high-level** — themes and priorities, not task-level granularity
- **Link to GitHub for real-time detail** — Issues/Milestones are the source of truth for current status
- **Be honest about pre-v1.0 status** — the project is in active development, roadmap reflects that
- **Avoid hard dates** — use relative priority (Current Focus / Planned / Completed / Future Considerations)
- **Encourage participation** — the roadmap is not just informational, it invites community input
- **Be self-contained (FR43)** — the page must make sense to someone landing on it directly, not just via README navigation

### Current Project State (for roadmap content)

**Completed** (use these human-readable names on the roadmap page, NOT epic numbers):

- **Core event sourcing server** — CQRS command processing, DDD aggregate pattern with pure-function `Handle(Command, State?) → DomainResult`, event persistence with atomic writes, snapshots, and state rehydration
- **Command API gateway** — REST endpoints with JWT authentication, FluentValidation, MediatR pipeline, rate limiting, OpenAPI/Swagger, RFC 7807 error responses, optimistic concurrency
- **Event distribution** — CloudEvents 1.0 publishing, per-tenant per-domain topic isolation, at-least-once delivery with DAPR retry policies, persist-then-publish resilience, dead-letter routing
- **Multi-tenant security** — DAPR access control policies, data-path isolation, pub/sub topic isolation, security audit logging, payload protection
- **Observability and operations** — End-to-end OpenTelemetry tracing, structured logging, health/readiness endpoints, dead-letter-to-origin tracing
- **Sample application and CI/CD** — Counter domain example, DAPR component configs, integration tests (Tier 1-3), GitHub Actions CI/CD, NuGet publishing, Aspire deployment manifests
- **Fluent Client SDK API** — Convention engine with `[EventStoreDomain]` attribute, assembly scanner with auto-discovery, `AddEventStore()`/`UseEventStore()` extension methods, five-layer cascading configuration
- **Documentation foundation** — README with progressive disclosure, quickstart guide, concept deep dives (architecture, command lifecycle, event envelope, identity scheme), DAPR trade-offs FAQ intro, NuGet packages guide, awesome-event-sourcing ecosystem page, community infrastructure (contributing guide, issue/PR templates, GitHub Discussions)

**In Progress (Epics 14-15):**

- Epic 14: Deployment & operations guides (Docker Compose, Kubernetes, Azure Container Apps, DAPR component reference, security model, troubleshooting, disaster recovery) — all stories done
- Epic 15: Configuration, versioning & lifecycle — 15-1 through 15-3 done, 15-4 in review, 15-5 (this story), 15-6 remaining

**Planned (Epic 17):**

- Actor-Based Authorization & Query API — authorization options, validator abstractions, query contracts, projection actors, validation endpoints, integration tests

### Key File Locations

| File | Purpose |
|------|---------|
| `docs/community/roadmap.md` | **NEW** — target file |
| `docs/community/awesome-event-sourcing.md` | Sibling page in same folder — follow same template |
| `README.md` | Add roadmap link to Community section |
| `docs/fr-traceability.md` | Update FR33 GAP → COVERED |
| `.markdownlint-cli2.jsonc` | Linting config to validate against |
| `CHANGELOG.md` | Link from versioning section |
| `docs/guides/upgrade-path.md` | Link from versioning section |
| `CONTRIBUTING.md` | Link from "How to Influence" section |

### GitHub URLs for the Roadmap Page

- Repository: `https://github.com/Hexalith/Hexalith.EventStore`
- Issues: `https://github.com/Hexalith/Hexalith.EventStore/issues`
- Milestones: `https://github.com/Hexalith/Hexalith.EventStore/milestones`
- Discussions: `https://github.com/Hexalith/Hexalith.EventStore/discussions`

### README.md Current Community Section

The roadmap link should be added to this section:

```markdown
### Community

- [Awesome Event Sourcing](docs/community/awesome-event-sourcing.md) — curated ecosystem resources
- [GitHub Discussions](https://github.com/Hexalith/Hexalith.EventStore/discussions) — questions, ideas, and community support
- [Issue Tracker](https://github.com/Hexalith/Hexalith.EventStore/issues) — bug reports and feature requests
```

Add: `[Product Roadmap](docs/community/roadmap.md) — planned features and project direction`

### FR Traceability Update Details

Current state of FR33 row (line ~95 in fr-traceability.md):

```text
| FR33 | A developer can view the public product roadmap | `GAP` | — | Epic 15, Phase 2 (story 15-5) |
```

Update to:

```text
| FR33 | A developer can view the public product roadmap | `COVERED` | [roadmap.md](community/roadmap.md) | Public product roadmap |
```

Also update:

- Summary: Gap count 17 → 16
- Summary: Coverage percentage 73% → 75% (recalculate: 44 covered / 63 total with partial+self-ref = 47/63)
- Gap analysis: Epic 15 section header "(4 gaps)" → "(3 gaps)"
- Gap analysis: Remove FR33 row from Epic 15 table

**Cross-dependency warning:** Story 15-4 (upgrade-path-documentation, currently in `review`) also updates fr-traceability.md (FR52 GAP → COVERED). If 15-4 merges before 15-5 is implemented, the gap counts in fr-traceability will already be different (16 gaps, not 17). **The dev agent MUST read the actual current values in fr-traceability.md at implementation time** rather than blindly applying the numbers above. Decrement gap count by 1 and recalculate coverage from whatever the current state is.

### Project Structure Notes

- Target file: `docs/community/roadmap.md` (new file)
- `docs/community/` folder exists with `awesome-event-sourcing.md` — follow same conventions
- Back-link uses `../../README.md` (two levels up from `docs/community/`)
- Mermaid diagrams optional but welcome for visual roadmap timeline

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 8.5]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR33]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#Journey 4 — Kenji Contributes]
- [Source: docs/fr-traceability.md#FR33 — currently GAP]
- [Source: _bmad-output/implementation-artifacts/sprint-status.yaml — story 15-5-public-product-roadmap: backlog]
- [Source: docs/community/awesome-event-sourcing.md — sibling page template reference]
- [Source: _bmad-output/implementation-artifacts/15-4-upgrade-path-documentation.md — previous story learnings]

### Previous Story Intelligence (from Story 15-4)

- **Page template:** back-link `[← Back to Hexalith.EventStore](../../README.md)`, H1, intro paragraph, prerequisites blockquote, content sections, Next Steps footer
- **markdownlint-cli2** must pass with project config (`.markdownlint-cli2.jsonc`)
- **Branch pattern:** `docs/story-15-5-public-product-roadmap`
- **Commit pattern:** `feat(docs): Add public product roadmap (Story 15-5)`
- **Internal links:** All internal links must resolve to existing files
- **Cross-reference updates** are part of the story (update README Community section)
- **Code blocks** need language hints for syntax highlighting
- **FR traceability** update is required (FR33: GAP → COVERED)
- All doc stories: feature branch per story, single commit with `feat(docs):` prefix, merge via PR

### Git Intelligence

Recent commits show consistent documentation pattern:

```text
1d4eb5f docs: complete Story 15 documentation suite (#95)
b9e7897 fix: use VersionOverride for FluentUI packages in design directions prototype (#94)
a201d73 Merge pull request #93 from Hexalith/fix/sln-to-slnx-references
f825bf9 fix: replace .sln references with .slnx across docs
cf5d0bf Merge pull request #92 from Hexalith/docs/story-15-2-ready-for-dev
```

All doc stories follow: feature branch → single commit with `feat(docs):` prefix → merge via PR.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

### Completion Notes List

- Created `docs/community/roadmap.md` with all required sections: Current Focus, Planned (near-term + future considerations), Completed (human-readable names), How to Influence the Roadmap (contribute funnel), Versioning and Release Cadence, Next Steps footer
- Page follows standard template: back-link, H1, summary paragraph, tip callout, content sections, Next Steps
- Updated README.md Community section with Product Roadmap link (first position)
- Updated FR traceability for the current workspace state: FR33 is now `COVERED`, and the summary currently reads 46 covered, 14 gaps, 78% phase coverage (49/63 covered, partial, or self-referential)
- Removed `docs/community/.gitkeep` (no longer needed)
- Updated the roadmap review follow-up: Current Focus now matches the sprint state, Planned only lists not-yet-covered deployment and lifecycle work, and the contribution funnel links directly to the GitHub Discussions Ideas category
- markdownlint-cli2 passes with 0 errors on the story-owned Markdown files
- All internal links verified to resolve to existing files
- Current workspace still contains unrelated documentation review changes from stories 15-3 and 15-4; those files are intentionally excluded from this story's deliverables

### Change Log

- 2026-03-10: Implemented story 15-5 — created public product roadmap page, updated README cross-reference, updated FR traceability (FR33 COVERED), removed .gitkeep
- 2026-03-10: Review follow-up — aligned roadmap content with sprint reality, corrected the Discussions Ideas link, updated the story audit trail, and marked the story done

### File List

- `docs/community/roadmap.md` — NEW, then MODIFIED: public product roadmap page with review follow-up fixes for current focus, planned work, and Ideas-category linking
- `README.md` — MODIFIED: added Product Roadmap link to Community section
- `docs/fr-traceability.md` — MODIFIED: FR33 GAP→COVERED, updated summary counts and Epic 15 gap table
- `docs/community/.gitkeep` — DELETED: no longer needed
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — MODIFIED: story status synced to `done`
- `_bmad-output/implementation-artifacts/15-5-public-product-roadmap.md` — MODIFIED: status updated to `done`, review fixes recorded, audit trail synced to current workspace state

**Unrelated workspace changes intentionally excluded from this story's deliverables:** `docs/concepts/event-envelope.md`, `docs/concepts/event-versioning.md`, `docs/guides/upgrade-path.md`, `_bmad-output/implementation-artifacts/15-3-event-versioning-and-schema-evolution-guide.md`, `_bmad-output/implementation-artifacts/15-4-upgrade-path-documentation.md`

## Senior Developer Review (AI)

### Reviewer Model Used

GitHub Copilot (GPT-5.4)

### Outcome

All HIGH and MEDIUM review findings resolved. The roadmap now matches the current sprint state, the contribution funnel points to the actual Ideas category, the story audit trail reflects current workspace reality, and sprint tracking is synced.

### Review Notes

- [x] (AI-Review/HIGH) `docs/community/roadmap.md` now reflects the current sprint state: Epic 15 is in final review/FAQ-closeout mode, while Epic 17 remains the active engineering track.
- [x] (AI-Review/HIGH) The `Planned` section now lists only work that is still genuinely upcoming, instead of mixing already-covered troubleshooting and disaster recovery docs into future scope.
- [x] (AI-Review/HIGH) The contribution funnel now links directly to the GitHub Discussions Ideas category at `https://github.com/Hexalith/Hexalith.EventStore/discussions/categories/ideas`, satisfying the checked task as written.
- [x] (AI-Review/MEDIUM) The story record now explicitly documents unrelated workspace changes from stories 15-3 and 15-4 so the File List remains scoped and reviewable.
- [x] (AI-Review/MEDIUM) Completion notes and file tracking now describe the current workspace summary state accurately, and sprint tracking has been synced from `review` to `done`.
