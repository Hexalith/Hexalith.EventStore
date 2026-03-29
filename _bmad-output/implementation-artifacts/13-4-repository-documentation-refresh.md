# Story 13.4: Repository Documentation Refresh

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer,
I want all documentation aligned with the actual implemented surface area,
So that docs, planning artifacts, and code tell the same story.

## Acceptance Criteria

1. **Given** the README, **When** refreshed, **Then** it reflects the current product surface including query/projection/SignalR entry points (SCP-Docs).
2. **Given** `docs/reference/nuget-packages.md`, **When** updated, **Then** it lists 6 packages (including `Hexalith.EventStore.SignalR`) with correct descriptions (SCP-Docs).
3. **Given** `docs/community/roadmap.md`, **When** updated, **Then** it clearly distinguishes implemented capabilities from planned future work (SCP-Docs).
4. **Given** planning artifacts (PRD, architecture), **When** selectively corrected, **Then** factual statements about shipped behavior match the current repository state (SCP-Docs).

## Tasks / Subtasks

- [x] Task 0: Prerequisites (AC: #1-#4)
    - [x] 0.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` -- confirm baseline compiles (0 errors, 0 warnings)
    - [x] 0.2 Inventory current documentation state: read `README.md`, `docs/reference/nuget-packages.md`, `docs/community/roadmap.md`, `docs/guides/upgrade-path.md`
    - [x] 0.3 Read `docs/page-template.md` to confirm page structure conventions still apply

- [x] Task 1: README refresh (AC: #1)
    - [x] 1.1 Rewrite line 71 to position query/projection/SignalR as first-class product capabilities, not sample-only features. Replace "Today the sample topology also includes a query endpoint, preflight authorization endpoints, projection invalidation hooks, and optional SignalR notifications for real-time read-model refresh." with product-level language that flows from the `AddEventStore()` / `UseEventStore()` code block above it. Suggested: "Beyond commands, the platform includes a query pipeline with ETag-based cache validation, projection invalidation hooks, preflight authorization endpoints, and optional SignalR notifications for real-time read-model refresh."
    - [x] 1.2 If `docs/index.md` exists (created by Story 13-3), add a link to it from README's Documentation section header: "Full documentation index: [Documentation](docs/index.md)"
    - [x] 1.3 Verify all existing links in the Documentation section still point to valid files and have accurate descriptions
    - [x] 1.4 Verify the architecture Mermaid diagram and text description accurately reflect the current system (query path, SignalR, projections) -- the diagram already shows these, so likely just a verification pass

- [x] Task 2: Verify nuget-packages.md (AC: #2)
    - [x] 2.1 Confirm the page lists exactly 6 packages: Contracts, Client, Server, SignalR, Testing, Aspire
    - [x] 2.2 Cross-check each package description against its `.csproj` `<Description>` element for accuracy
    - [x] 2.3 Cross-check the dependency graph against actual `<PackageReference>` entries in each `.csproj`
    - [x] 2.4 Cross-check external dependency versions against `Directory.Packages.props` -- update if any versions have drifted
    - [x] 2.5 Document verification results (accurate / corrections made) in the Dev Agent Record

- [x] Task 3: Verify roadmap.md (AC: #3)
    - [x] 3.1 Read `docs/community/roadmap.md` and verify:
        - "Completed" section includes all shipped capabilities (core event sourcing, command API, query/projection API, SignalR, event distribution, multi-tenant security, observability, sample app, fluent client SDK, documentation foundation)
        - "Current Focus" accurately describes active work
        - "Planned" section does NOT list any capability that is already implemented
        - "Future Considerations" lists only genuinely unimplemented capabilities
    - [x] 3.2 If any misclassification found, move the item to the correct section
    - [x] 3.3 Update "Current Focus" if needed to reflect the current sprint state (Epic 13 documentation is being wrapped up)
    - [x] 3.4 Document verification results in the Dev Agent Record

- [x] Task 4: Verify and update docs pages for accuracy (AC: #1, #4)
    - [x] 4.1 Read `docs/guides/upgrade-path.md` and verify it shows 6 packages (already confirmed in analysis -- should say "6 NuGet packages" on line 120)
    - [x] 4.2 Read `docs/concepts/architecture-overview.md` and verify it covers the query/projection/SignalR path (already does -- verification pass)
    - [x] 4.3 Spot-check 3-5 other docs pages for factual accuracy against current code (e.g., `docs/concepts/command-lifecycle.md`, `docs/concepts/identity-scheme.md`, `docs/guides/configuration-reference.md`, `docs/getting-started/first-domain-service.md`)
    - [x] 4.4 Fix any factual inaccuracies found; document findings in Dev Agent Record

- [x] Task 5: Planning artifacts selective correction (AC: #4)
    - [x] 5.1 **Grep-first approach** on `_bmad-output/planning-artifacts/prd.md` (do NOT read cover-to-cover -- the file is 60+ pages). Grep for these specific divergence indicators and verify each match:
        - `"5 packages"` or `"five packages"` (should say 6)
        - `"EventEnvelope"` field lists and counts (check against `EventEnvelope.cs` / `EventMetadata.cs`)
        - API endpoint paths like `/api/v1/` (should match actual controllers)
        - `"SignalR"` mentions (verify they match shipped capability)
        - Query contract type names (check against actual query types)
    - [x] 5.2 **Grep-first approach** on `_bmad-output/planning-artifacts/architecture.md`. Grep for:
        - Technology versions (DAPR SDK, .NET, Aspire versions vs. `Directory.Packages.props`)
        - File paths and project names
        - Implementation details that no longer match shipped code
    - [x] 5.3 For each divergence found: if the planning doc states something as currently implemented that is NOT in the code, add a clear annotation or correction (e.g., "Note: As of the current release, this is planned -- not yet implemented")
    - [x] 5.4 Do NOT rewrite planning artifacts wholesale -- make surgical, targeted corrections only
    - [x] 5.5 Document all corrections made with before/after in the Dev Agent Record

- [x] Task 6: Build and test (AC: #1-#4)
    - [x] 6.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` -- 0 errors, 0 warnings
    - [x] 6.2 Run all Tier 1 tests -- 0 regressions
    - [x] 6.3 Verify no broken relative links in modified docs (grep for `](` patterns and spot-check paths exist)

## Dev Notes

### CRITICAL: This Is Primarily a Documentation Verification and Alignment Story

Most of the SCP-Docs issues have already been resolved by prior stories. The main remaining work is:

1. **README line 71** — The only significant edit: reposition query/projection/SignalR from "sample topology also includes..." to first-class product language
2. **Verification passes** — nuget-packages.md, roadmap.md, upgrade-path.md, and architecture-overview.md already appear accurate based on analysis; this story confirms that and documents the verification
3. **Planning artifact corrections** — Selective corrections where PRD or architecture docs make factual claims about shipped behavior that don't match the codebase

### What's Already Correct (Verified During Story Creation)

| Document                                 | Status   | Notes                                                                                                        |
| ---------------------------------------- | -------- | ------------------------------------------------------------------------------------------------------------ |
| `docs/reference/nuget-packages.md`       | Accurate | 6 packages listed with correct descriptions, dependency graph matches `.csproj` files                        |
| `docs/community/roadmap.md`              | Accurate | Completed section lists query/projection/SignalR correctly; planned section has no already-implemented items |
| `docs/guides/upgrade-path.md`            | Accurate | Line 120 says "6 NuGet packages", lists all 6 in table                                                       |
| `docs/concepts/architecture-overview.md` | Accurate | Covers query path (lines 246-252), SignalR hub (line 147), projection changes (line 63)                      |
| README architecture diagram              | Accurate | Shows SignalR, query handling, projections in Mermaid diagram                                                |
| README Documentation section             | Accurate | All 14+ links verified to existing files                                                                     |

### What Needs Changing

| Document            | Issue                                                                                                                          | Fix                               |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------ | --------------------------------- |
| `README.md` line 71 | Says "Today the sample topology also includes..." — positions query/projection/SignalR as sample add-ons, not product features | Rewrite to product-level language |
| `README.md`         | Missing link to `docs/index.md` (if created by Story 13-3)                                                                     | Add link if file exists           |
| Planning artifacts  | May contain factual divergences from shipped code                                                                              | Surgical corrections only         |

### SCP-Docs Sprint Change Proposal Context

This story implements the remaining scope of `SCP-Docs` (Sprint Change Proposal from 2026-03-15). Key items from that proposal:

- **Proposal A1 (README refresh):** Partially done — Mermaid diagram and Documentation section are accurate; line 71 phrasing still needs update
- **Proposal A2 (Quickstart consistency):** Already addressed by Story 13-1
- **Proposal B1 (Query/projection docs):** Already addressed — `docs/reference/query-api.md` and `docs/concepts/architecture-overview.md` cover the query/projection/SignalR path comprehensively
- **Proposal B2 (Package guide correction):** Already fixed — all docs say 6 packages
- **Proposal B3 (Architecture/roadmap refresh):** Architecture overview is accurate; roadmap is accurate; this story verifies
- **Proposal C1 (Planning artifact alignment):** Still needed — the PRD and architecture doc may have factual statements ahead of or behind the code

### Page Template Conventions (from `docs/page-template.md`)

Every doc page MUST:

- Start with a back-link: `[<- Back to Hexalith.EventStore](../../README.md)`
- Have exactly one H1 heading (page title)
- Have a one-paragraph summary after the title
- End with "Next Steps" footer with "Next:" and "Related:" links
- Use kebab-case filenames, no YAML frontmatter
- Use language-tagged code blocks (`csharp`, `bash`, `yaml`)

### Editing Scope

**Files you MUST edit:**

- `README.md` -- rewrite line 71, add `docs/index.md` link if it exists

**Files you MUST verify (edit only if inaccurate):**

- `docs/reference/nuget-packages.md` -- verify 6 packages, descriptions, dependency versions
- `docs/community/roadmap.md` -- verify implemented vs. planned classification
- `docs/guides/upgrade-path.md` -- verify package count and version table
- `docs/concepts/architecture-overview.md` -- verify query/projection/SignalR coverage

**Files you MAY edit (surgical corrections only):**

- `_bmad-output/planning-artifacts/prd.md` -- correct factual divergences from shipped code
- `_bmad-output/planning-artifacts/architecture.md` -- correct factual divergences from shipped code
- Any other docs page where a factual inaccuracy is found during spot-checks

**Files you MUST NOT edit:**

- Any C# source files
- `docs/reference/api/**/*.md` (auto-generated)
- `docs/reference/problems/**/*.md` (created by Story 13-2)
- `docs/page-template.md` (conventions doc)
- `docs/assets/**` (media files)
- `_bmad-output/planning-artifacts/epics.md` (epic definitions, not to be corrected in this story)

### What NOT to Do

- Do NOT modify any source code -- this is documentation alignment only
- Do NOT restructure documentation pages (that's Story 13-3's scope)
- Do NOT rewrite entire planning artifacts -- make surgical factual corrections only
- Do NOT add new documentation pages (unless absolutely required for a gap)
- Do NOT change file names of existing docs pages (would break external links)
- Do NOT add unit tests -- there is no testable code change
- Do NOT change the Mermaid diagram in README -- it's already accurate

### Branch Base Guidance

Branch from `main`. Branch name: `docs/story-13-4-repository-documentation-refresh`

### Previous Story Intelligence

**Story 13-3 (Progressive Documentation Structure) scope:**

- Creates `docs/index.md` -- documentation home page with progressive tier organization
- Adds projection type naming section to `docs/reference/query-api.md`
- Audits all docs for level mixing
- If 13-3 completes before 13-4, the `docs/index.md` will exist and README should link to it. If not, skip that sub-task.

**Story 13-2 (Error Reference Pages at Type URIs) learnings:**

- Documentation-only story, 14 new Markdown files created
- Source of truth principle: codebase is always right, docs must match code
- Page template strictly followed: back-link, H1, summary, prerequisites, content sections, Next Steps footer
- Writing style: scannable, not essays. Short sentences, bullet points, code blocks
- Build: 0 errors, 0 warnings. 724 Tier 1 tests pass
- Branch naming convention: `docs/story-13-2-error-reference-pages-at-type-uris`
- Commit message style: `docs: <description> (Story 13-2)`

**Story 13-1 (Quick Start Guide) learnings:**

- Source of truth principle applied: found and fixed gaps between docs and code
- Two gaps found: missing `messageId` field, missing Aspire CLI prerequisite
- Verified quickstart stays at hands-on level

### Git Intelligence

Recent commits show Stories 13-1 and 13-2 completed:

- `67c9bd2` Merge PR #132: docs/story-13-2-error-reference-pages-at-type-uris
- `6f8d4e6` docs: Add error reference pages at type URIs (Story 13-2)
- `b17d298` Merge PR #131: docs/story-13-1-quick-start-guide
- `3eca00e` docs: Update quick start guide with Aspire CLI prerequisite and messageId field (Story 13-1)
- `cca2660` Merge PR #130: feat/story-12-2-interactive-command-buttons-on-all-pattern-pages

Epic 13 is in-progress. Stories 13-1 and 13-2 done. Story 13-3 in review. Codebase stable on main. Always check current git state -- do not rely on this snapshot.

### Architecture Compliance

- **Solution file:** `Hexalith.EventStore.slnx` (modern XML format)
- **Build command:** `dotnet build Hexalith.EventStore.slnx --configuration Release`
- **Warnings as errors:** enabled -- build must produce 0 warnings
- **Published packages (6):** Contracts, Client, Server, SignalR, Testing, Aspire
- **Non-published projects (3):** AppHost, CommandApi, ServiceDefaults

### Testing Compliance

No new tests needed -- this is a documentation alignment story. Tier 1 tests run as regression verification only.

### Key Files for Cross-Referencing

| File                                                                                                    | Purpose for this story                   |
| ------------------------------------------------------------------------------------------------------- | ---------------------------------------- |
| `README.md`                                                                                             | Primary edit target (line 71)            |
| `docs/reference/nuget-packages.md`                                                                      | Verify 6 packages                        |
| `docs/community/roadmap.md`                                                                             | Verify implemented vs. planned           |
| `docs/guides/upgrade-path.md`                                                                           | Verify package count                     |
| `docs/concepts/architecture-overview.md`                                                                | Verify query/projection/SignalR coverage |
| `docs/page-template.md`                                                                                 | Page conventions reference               |
| `Directory.Packages.props`                                                                              | Authoritative dependency versions        |
| `.github/workflows/release.yml`                                                                         | Authoritative package list (6 expected)  |
| `_bmad-output/planning-artifacts/prd.md`                                                                | Planning artifact to selectively correct |
| `_bmad-output/planning-artifacts/architecture.md`                                                       | Planning artifact to selectively correct |
| `_bmad-output/planning-artifacts/sprint-change-proposal-2026-03-15-eventstore-documentation-refresh.md` | SCP-Docs full context                    |

### Project Structure Notes

- The SCP-Docs sprint change proposal (2026-03-15) is the authoritative source for this story's scope
- Many issues identified in SCP-Docs have already been fixed by prior stories and organic updates
- The remaining scope is focused: 1 README edit, verification passes, and surgical planning artifact corrections
- Story 13-3 (Progressive Documentation Structure) may or may not have been implemented before this story runs -- check for `docs/index.md` existence

### References

- [Source: _bmad-output/planning-artifacts/epics.md, Epic 13, Story 13.4 (lines 1542-1564)]
- [Source: _bmad-output/planning-artifacts/epics.md, SCP-Docs definition (line 258)]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-03-15-eventstore-documentation-refresh.md -- full SCP-Docs proposal]
- [Source: docs/page-template.md -- documentation conventions]
- [Source: README.md, line 71 -- the primary text that needs rewriting]
- [Source: docs/reference/nuget-packages.md -- package inventory (already accurate)]
- [Source: docs/community/roadmap.md -- feature status classification (already accurate)]
- [Source: docs/guides/upgrade-path.md, line 120 -- package count (already accurate)]

### Definition of Done

- AC #1 verified: README reflects current product surface with query/projection/SignalR positioned as first-class capabilities (not sample add-ons)
- If `docs/index.md` exists, README links to it
- AC #2 verified: `docs/reference/nuget-packages.md` lists 6 packages with correct descriptions and dependency versions
- AC #3 verified: `docs/community/roadmap.md` clearly distinguishes implemented from planned, with no misclassifications
- AC #4 verified: planning artifacts (PRD, architecture) have no factual statements contradicting shipped code
- All verification results documented in Dev Agent Record
- Build: `dotnet build Hexalith.EventStore.slnx --configuration Release` -- 0 errors, 0 warnings
- Tier 1 tests pass with 0 regressions
- No broken relative links in modified docs

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

### Completion Notes List

- **Task 0:** Build baseline confirmed (0 errors, 0 warnings). All docs inventoried and page template conventions verified.
- **Task 1:** README line 71 rewritten from "Today the sample topology also includes..." to "Beyond commands, the platform includes...". `docs/index.md` link already present (added by Story 13-3). All 18+ README links verified pointing to existing files. Mermaid diagram and text description verified accurate.
- **Task 2:** nuget-packages.md verified and corrected: 6 packages listed, SignalR client version updated to `10.0.5`, dependency graph fixed to show `Server -> Client` and package detail text corrected to reflect `Hexalith.EventStore.Contracts` depending on `Hexalith.Commons.UniqueIds` and `Hexalith.EventStore.Server` depending on both Client and Contracts.
- **Task 3:** roadmap.md had 3 misclassifications: (1) "Current Focus" referenced non-existent Epic 15 — corrected to Epic 13 documentation wrap-up; (2) "Planned → Near-Term" listed DAPR FAQ, deployment guides, and operational docs that are all already implemented — removed from planned; (3) Added DAPR FAQ and deployment/operations guides to Completed section.
- **Task 4:** upgrade-path.md verified: says "6 NuGet packages" on line 120, correct. architecture-overview.md verified: covers query path (lines 244-254), SignalR hub (line 147), projection changes. Spot-checked command-lifecycle.md, identity-scheme.md, configuration-reference.md — all factually accurate against current codebase.
- **Task 5 — PRD corrections:** (1) development strategy and query experience wording updated to reflect that query/projection caching and SignalR ship in the current release, not a future v2-only phase; (2) package distribution table corrected from 5 shipped packages to 6 by adding `Hexalith.EventStore.SignalR`; (3) command routes corrected to `/api/v1/commands/status/{correlationId}` and `/api/v1/commands/replay/{correlationId}`; (4) query routes corrected from `/api/v2/queries` variants to the shipped `POST /api/v1/queries` and `POST /api/v1/queries/validate` endpoints; (5) health endpoints corrected to `/health` and `/ready`; (6) DAPR runtime references updated from `1.14+` to `1.16.1`; (7) implemented query caching and SignalR rows rewritten so their descriptions no longer contradict the `IMPLEMENTED` verdict; (8) phase roadmap rows for already-shipped query/SignalR capabilities removed.
- **Task 5 — Architecture corrections:** (1) "5 packages" → "6 packages" in 3 locations, SignalR row added to package table; (2) Dapr.Client version 1.16.0 → 1.16.1; (3) Aspire version 13.1.0 → 13.1.2; (4) CommunityToolkit.Aspire.Hosting.Dapr 9.7.0 → 13.0.0.
- **Task 6:** Final build: 0 errors, 0 warnings. Tier 1 tests: 724 passed, 0 failed. All relative links in modified docs verified.
- **Review remediation (2026-03-29):** Fixed the stale `POST /api/commands` sequence-diagram example in `docs/concepts/architecture-overview.md`, relabeled the remaining query-pipeline sections in `prd.md` from `(v2)` to `(current release)`, updated Journey 7 to reference the current release, and restored the `Post-MVP Features` heading hierarchy by nesting the phase headings under that parent section.

### Change Log

- 2026-03-21: Story 13.4 implemented — README refresh, nuget-packages.md version fix, roadmap.md misclassification corrections, planning artifact surgical corrections (PRD + architecture)
- 2026-03-29: Review remediation completed — architecture overview route example corrected and remaining PRD label/hierarchy cleanup applied

### File List

- `README.md` — line 71 rewritten (product-level language for query/projection/SignalR)
- `docs/concepts/architecture-overview.md` — stale sequence-diagram command route corrected to `/api/v1/commands`
- `docs/reference/nuget-packages.md` — SignalR client version corrected, dependency graph aligned with `.csproj` references, Contracts external dependency documented
- `docs/community/roadmap.md` — Current Focus updated, Planned items moved to Completed
- `_bmad-output/planning-artifacts/prd.md` — surgical corrections for shipped package count, command/query routes, DAPR version, query/SignalR implementation status, remaining current-release labeling, and heading hierarchy under Post-MVP Features
- `_bmad-output/planning-artifacts/architecture.md` — 7 surgical corrections (package count, version numbers)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — story status updated
- `_bmad-output/implementation-artifacts/13-4-repository-documentation-refresh.md` — task checkboxes, dev agent record

### Review Findings

- [x] `[Review][Patch]` Fix the stale command route/example that was missed in `architecture-overview.md` verification [docs/concepts/architecture-overview.md:246]
- [x] `[Review][Patch]` Finish the PRD relabeling from query features as “v2” to query features in the current release [_bmad-output/planning-artifacts/prd.md:327]
- [x] `[Review][Patch]` Revert non-surgical PRD structure churn and restore a valid heading hierarchy [_bmad-output/planning-artifacts/prd.md:192]
<!-- End of story file -->

