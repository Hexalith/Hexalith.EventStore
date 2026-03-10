# Story 15.4: Upgrade Path Documentation

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer upgrading between Hexalith versions,
I want a documented upgrade path with migration steps,
so that I can move between major versions safely.

## Acceptance Criteria

1. **Given** a developer needs to upgrade from one version to another **When** they consult the upgrade documentation **Then** the CHANGELOG (`CHANGELOG.md`, Story 1.6) documents breaking changes per release with migration steps
2. **And** a dedicated page at `docs/guides/upgrade-path.md` explains the general upgrade procedure: check CHANGELOG, update NuGet packages, run tests, handle breaking changes
3. **And** the content links to `CHANGELOG.md` for version-specific details
4. **And** the page follows the standard page template: back-link `[← Back to Hexalith.EventStore](../../README.md)`, H1, summary paragraph, prerequisites callout, content sections, Next Steps footer
5. **And** the README Guides section links to the upgrade path page
6. **And** `docs/fr-traceability.md` is updated: FR52 from `GAP` to `COVERED` referencing `docs/guides/upgrade-path.md`
7. **And** markdownlint-cli2 passes with project config (`.markdownlint-cli2.jsonc`)

## Tasks / Subtasks

- [x] Task 1: Create `docs/guides/upgrade-path.md` (AC: #1, #2, #3, #4)
    - [x] 1.1 Write page following standard template: back-link `[← Back to Hexalith.EventStore](../../README.md)`, H1 "Upgrade Path", summary paragraph, prerequisites callout linking to `CHANGELOG.md` and `event-versioning.md`
    - [x] 1.2 Section: "Before You Upgrade" — pre-upgrade checklist with concrete commands
    - [x] 1.3 Section: "General Upgrade Procedure" — two paths (fast/full) with Mermaid flowchart, upgrade order, auditing custom code subsection
    - [x] 1.4 Section: "NuGet Package Updates" — centralized management, 5 packages, warning about mixed versions
    - [x] 1.5 Section: "Dependency Compatibility Matrix" — version table, DAPR compatibility subsection
    - [x] 1.6 Section: "Event Envelope Schema Changes" — backward compatibility guarantee, snapshot compatibility
    - [x] 1.8 Section: "Testing Your Upgrade" — 3-tier strategy, commands, warning callout, failure signal table
    - [x] 1.9 Section: "Rollback Strategy" — revert packages, redeploy, event stream safety, disaster recovery link
    - [x] 1.9a Section: "TL;DR Quick Reference" — 5 numbered steps near top of page
    - [x] 1.10 Next Steps footer: links to CHANGELOG, event-versioning, configuration-reference, troubleshooting, disaster-recovery
- [x] Task 2: Update README.md (AC: #5)
    - [x] 2.1 Add `[Upgrade Path](docs/guides/upgrade-path.md) — migrating between versions` to the Guides section in README.md
- [x] Task 3: Update FR traceability (AC: #6)
    - [x] 3.1 Update `docs/fr-traceability.md` — set FR52 from `GAP` to `COVERED` referencing `docs/guides/upgrade-path.md`
    - [x] 3.2 Update gap summary counts (17 gaps → 16)
    - [x] 3.3 Update coverage percentage (73% → 75%)
- [x] Task 4: Validate with markdownlint-cli2 (AC: #7)
    - [x] 4.1 Run `npx markdownlint-cli2 docs/guides/upgrade-path.md` — 0 errors
    - [x] 4.2 Verify all internal links resolve to existing files — all 8 link targets confirmed

## Dev Notes

### Architecture Context: Versioning and Upgrade Strategy

Hexalith.EventStore uses **MinVer 7.0.0** for git tag-based SemVer (prefix `v`). All 5 NuGet packages (Contracts, Client, Server, Testing, Aspire) share the same version derived from git tags. Centralized package management via `Directory.Packages.props` means developers update dependency versions in one file.

**Semantic Versioning contract:**

- MAJOR: Breaking API changes OR event envelope schema changes
- MINOR: New features, backward-compatible
- PATCH: Bug fixes only

**Event envelope schema guarantee (Architecture.md):** Envelope schema changes between Hexalith versions are treated as MAJOR version bumps. EventStore guarantees backward-compatible reading of all previously persisted envelopes. This means upgrading never breaks existing event streams.

### Current Dependency Versions (from Directory.Packages.props)

| Dependency       | Version  | Notes                                               |
| ---------------- | -------- | --------------------------------------------------- |
| .NET SDK         | 10.0.103 | Pinned in `global.json`, `rollForward: latestPatch` |
| DAPR SDK         | 1.16.1   | Client, AspNetCore, Actors                          |
| .NET Aspire      | 13.1.x   | Hosting and ServiceDefaults                         |
| MediatR          | 14.0.0   | CQRS pipeline                                       |
| FluentValidation | 12.1.1   | Command validation                                  |
| OpenTelemetry    | 1.15.0   | Observability                                       |

### AC1 Clarification: Precondition, Not Deliverable

AC1 ("CHANGELOG documents breaking changes per release with migration steps") was **already delivered by Story 1.6** (CHANGELOG initialization, Epic 8). This story's responsibility is to **LINK to the CHANGELOG** (AC3), not to create or modify it. Dev agent should NOT edit `CHANGELOG.md`.

### Content Strategy: Complement, Don't Duplicate

- `CHANGELOG.md` — version-specific breaking changes and migration steps (already exists, [Unreleased] section)
- `docs/concepts/event-versioning.md` — event _payload_ schema evolution strategies
- This page (`upgrade-path.md`) — the _general procedure_ for upgrading between Hexalith versions (package-level, not event-level)

The upgrade path page MUST:

- **Link** to `CHANGELOG.md` for version-specific details — do NOT duplicate per-version changes
- **Link** to `event-versioning.md` for event schema evolution — do NOT duplicate that content
- **Add new value**: the general upgrade procedure, dependency compatibility, testing strategy, rollback plan
- **Be practical**: step-by-step, actionable, with real commands

### Key Files to Reference

- `Directory.Packages.props` — centralized NuGet version management
- `global.json` — .NET SDK version pin
- `CHANGELOG.md` — breaking changes per release
- `docs/concepts/event-versioning.md` — event schema evolution
- `docs/guides/configuration-reference.md` — configuration options
- `docs/guides/dapr-component-reference.md` — DAPR component configs
- `docs/guides/troubleshooting.md` — common issues and solutions

### PRD User Journey Context

From PRD "Journey 5 — Marco Returns" (FR52):

> Marco sees the release notification. He opens the GitHub repo. He clicks CHANGELOG.md and sees a clear list of breaking changes with migration steps. He follows the upgrade path documentation. It tells him which NuGet packages to update, what code changes are needed, and how to handle existing event streams.

The page should feel like Marco's upgrade companion — practical, reassuring, and complete.

### CHANGELOG.md Current State

The CHANGELOG currently has only an `[Unreleased]` section with a comprehensive list of Added items. There are no released versions yet (no `## [X.Y.Z]` sections). The upgrade guide should reference the CHANGELOG structure and explain that version-specific migration steps will appear there with each release. The guide itself covers the _general_ upgrade procedure that applies to any version transition.

**CRITICAL: Pre-v1.0 authoring constraint.** The guide must be useful even before any major version is released — it establishes the upgrade _procedure contract_ that future CHANGELOGs will reference. Frame content as "when a new version is released, follow these steps" rather than referencing specific version transitions that don't exist yet.

### Upgrade Order Rationale

The recommended upgrade order is: .NET SDK → DAPR runtime → Hexalith NuGet packages. This follows the dependency chain — Hexalith depends on DAPR SDK which depends on .NET. Upgrading in reverse order may cause build failures (new Hexalith targets newer SDK) or runtime failures (new DAPR SDK calls APIs unavailable on old runtime).

### Snapshot Compatibility Note

Snapshots contain serialized aggregate state. When state shape changes between major versions (e.g., new fields added to aggregate state), old snapshots must remain deserializable. The Counter sample demonstrates the required pattern via `RehydrateCount()` which handles null, typed object, `JsonElement`, and enumerable representations. The upgrade guide should reference this in the event envelope section and link to `event-versioning.md`'s snapshot coverage.

### Key Files to Reference (additional from elicitation)

- `docs/guides/disaster-recovery.md` — backup and restore procedures (link from rollback section)

### Post-v1.0 Consideration

After the first major version is released, consider adding version-specific upgrade pages (e.g., `docs/guides/upgrade-v1-to-v2.md`) linked from the general guide for deep dives. Not needed for the initial version of this page — the general procedure guide is the correct starting point.

### Project Structure Notes

- Target file: `docs/guides/upgrade-path.md` (new file)
- Alignment: `docs/guides/` folder already contains related pages (configuration-reference.md, troubleshooting.md, deployment guides)
- Follows architecture D1 folder structure
- Mermaid diagrams encouraged for upgrade flow visualization

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 8.4]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR52]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#Journey 5 — Marco Returns]
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#Lifecycle & Versioning]
- [Source: docs/fr-traceability.md#FR52 — currently GAP]
- [Source: CHANGELOG.md — current content, [Unreleased] section only]
- [Source: Directory.Packages.props — centralized package versions]
- [Source: global.json — .NET SDK pin]
- [Source: docs/concepts/event-versioning.md — event schema evolution guide]
- [Source: docs/guides/configuration-reference.md — configuration reference]
- [Source: docs/guides/dapr-component-reference.md — DAPR component reference]
- [Source: _bmad-output/implementation-artifacts/15-3-event-versioning-and-schema-evolution-guide.md — previous story learnings]

### Previous Story Intelligence (from Story 15-3)

- **Page template:** back-link `[← Back to Hexalith.EventStore](../../README.md)`, H1, intro paragraph, prerequisites blockquote, content sections, Next Steps footer
- **markdownlint-cli2** must pass with project config (`.markdownlint-cli2.jsonc`)
- **Branch pattern:** `docs/story-15-4-upgrade-path-documentation`
- **Commit pattern:** `feat(docs): Add upgrade path documentation (Story 15-4)`
- **Internal links:** All internal links must resolve to existing files
- **Cross-reference updates** are part of the story (update README Guides section)
- **Code blocks** need language hints for syntax highlighting
- **FR traceability** update is required (FR52: GAP → COVERED)
- All doc stories: feature branch per story, single commit with `feat(docs):` prefix, merge via PR

### Git Intelligence

Recent commits show consistent documentation pattern:

```text
a201d73 Merge pull request #93 from Hexalith/fix/sln-to-slnx-references
f825bf9 fix: replace .sln references with .slnx across docs
cf5d0bf Merge pull request #92 from Hexalith/docs/story-15-2-ready-for-dev
b666ce3 docs: add Story 15-2 spec and mark ready-for-dev
a73bcd3 feat: Add IEventPayloadProtectionService for GDPR payload encryption (#91)
```

All doc stories follow: feature branch → single commit with `feat(docs):` prefix → merge via PR.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

### Completion Notes List

- Created `docs/guides/upgrade-path.md` with all specified sections: TL;DR Quick Reference, Before You Upgrade checklist, General Upgrade Procedure (fast/full paths with Mermaid flowchart), Auditing Custom Code, NuGet Package Updates (with mixed-version warning), Dependency Compatibility Matrix (with DAPR compatibility subsection), Event Envelope Schema Changes (with snapshot compatibility), Testing Your Upgrade (3-tier strategy with failure signal table), Rollback Strategy, and Next Steps footer
- Page follows standard template: back-link, H1, intro paragraph, prerequisites blockquote, content sections, Next Steps
- Content complements (not duplicates) CHANGELOG.md and event-versioning.md — links to both
- Pre-v1.0 framing: guide establishes upgrade procedure contract, useful before any major version exists
- Updated README.md: added Upgrade Path link to Guides section
- Updated docs/fr-traceability.md: FR52 GAP → COVERED, gap count 17 → 16, coverage 73% → 75%, removed FR52 from Epic 15 gap analysis table
- markdownlint-cli2 passes on all modified files (0 errors)
- All internal links verified to resolve to existing files
- All Tier 1 tests pass (465 total: 157 + 231 + 29 + 48) — no regressions
- Code review fixes applied: README now links to the guide, FR52 traceability now points at `docs/guides/upgrade-path.md`, and the story record no longer claims those cross-references were missing
- Resolved final review finding: AC #1 is a precondition (Story 1.6), not a deliverable — the upgrade guide links to CHANGELOG.md and is designed to work pre-v1.0
- All 4 review items resolved; story complete
- Review follow-up fixes applied: corrected the FR traceability summary/gap analysis after FR55 coverage, replaced the upgrade guide's repo-root package check with shell-specific commands that isolate Hexalith packages, and removed the incorrect Architecture Decision D3 reference from the public guide
- Current workspace still contains unrelated uncommitted changes outside Story 15.4 scope (`docs/concepts/event-envelope.md`, `docs/concepts/event-versioning.md`, `_bmad-output/implementation-artifacts/15-3-event-versioning-and-schema-evolution-guide.md`, `_bmad-output/implementation-artifacts/15-5-public-product-roadmap.md`); they are intentionally not listed as Story 15.4 deliverables

### Change Log

- 2026-03-10: Created upgrade path documentation page and updated cross-references (Story 15-4)
- 2026-03-10: Code review follow-up — fixed missing README guide link, updated FR52 traceability to COVERED, and set story back to in-progress pending released CHANGELOG migration entries
- 2026-03-10: Resolved final review finding (AC #1 precondition clarification) — all review items complete, story marked for review
- 2026-03-10: Code review follow-up — corrected FR traceability totals, fixed the upgrade guide's package-version check commands, removed the incorrect architecture decision reference, and marked the story done

### File List

- `docs/guides/upgrade-path.md` (new)
- `README.md` (modified — added Upgrade Path link to Guides section)
- `docs/fr-traceability.md` (modified — FR52 GAP → COVERED, updated counts)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified — story status)
- `_bmad-output/implementation-artifacts/15-4-upgrade-path-documentation.md` (modified — task checkboxes, Dev Agent Record, status)

### Review Notes

- [x] (AI-Review/HIGH) `README.md` now includes the required Guides entry for [Upgrade Path](../../../docs/guides/upgrade-path.md), satisfying AC #5.
- [x] (AI-Review/HIGH) `docs/fr-traceability.md` now marks FR52 as `COVERED` and links to `docs/guides/upgrade-path.md`, satisfying AC #6.
- [x] (AI-Review/HIGH) The story record now matches repo reality for the review fixes applied in this pass.
- [x] (AI-Review/HIGH) AC #1 is a **precondition** delivered by Story 1.6, not a deliverable of this story (see Dev Notes: "AC1 Clarification: Precondition, Not Deliverable"). The upgrade guide correctly links to CHANGELOG.md (AC #3) and is designed to work pre-v1.0 per the "Pre-v1.0 authoring constraint". No action needed — resolved by design.
- [x] (AI-Review/MEDIUM) `docs/fr-traceability.md` summary and Epic 14 gap analysis now match the covered FR rows after FR55 coverage moved from gap to covered.
- [x] (AI-Review/MEDIUM) `docs/guides/upgrade-path.md` now uses shell-specific commands that isolate Hexalith package entries instead of a repo-root `grep Hexalith` check that matches project names.
- [x] (AI-Review/MEDIUM) `docs/guides/upgrade-path.md` no longer cites the incorrect Architecture Decision D3 reference for envelope schema versioning.
- [x] (AI-Review/MEDIUM) Remaining workspace diffs outside Story 15.4 scope are explicitly documented as unrelated, so the story file list remains scoped to this story's deliverables.
