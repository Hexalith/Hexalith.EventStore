# Story 8.4: Choose the Right Tool Decision Aid

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer evaluating event sourcing solutions,
I want a structured decision aid that helps me assess whether Hexalith fits my project,
so that I can make an informed technology choice before investing time.

## Acceptance Criteria

1. **AC1 - Comparison Table (FR4)**: The page at `docs/concepts/choose-the-right-tool.md` includes a detailed comparison of Hexalith vs Marten (Critter Stack), EventStoreDB (KurrentDB), and custom/DIY implementations across dimensions: infrastructure portability, multi-tenancy, CQRS framework completeness, deployment model, database lock-in, projection system, licensing, maturity, and operational complexity

2. **AC2 - Decision Aid (FR3)**: The page includes a structured self-assessment section (decision flowchart rendered as a numbered question sequence in markdown, since Mermaid flowcharts can become unwieldy) that guides the developer through key questions: .NET stack?, infrastructure portability needed?, multi-tenant?, already on PostgreSQL?, need maximum performance?, need polyglot SDKs?, LINQ querying needed? — each question points to the recommended tool

3. **AC3 - When NOT to Use Hexalith (FR16)**: The page honestly describes specific scenarios where Hexalith is NOT the right choice, including: non-.NET stacks, sub-millisecond latency requirements, no container orchestration available, already invested in PostgreSQL everywhere, need LINQ querying over events, need polyglot SDKs, team unwilling to adopt DAPR, need battle-tested production maturity today

4. **AC4 - Limitation-Strength Pairing**: Every stated Hexalith limitation is paired with a corresponding strength — framed as informed decision-making, not self-deprecation (per Innovation #2 Radical Transparency)

5. **AC5 - Page Template Compliance (D7)**: The page follows the standard page template: back-link to README (`[← Back to Hexalith.EventStore](../../README.md)`), H1 title, one-paragraph summary, content sections, Next Steps footer

6. **AC6 - Self-Contained (FR43)**: A developer arriving from a search engine understands the page without reading other pages first — Hexalith is briefly introduced in context before comparisons begin

7. **AC7 - Heading Hierarchy (NFR6)**: Page uses H1 → H2 → H3 hierarchy with no skipped levels

8. **AC8 - Relative Links Only**: All internal links use relative paths (no absolute URLs for internal documentation)

9. **AC9 - No YAML Frontmatter**: Page does NOT use YAML frontmatter

10. **AC10 - Factual Accuracy**: Competitor comparisons are factually accurate and verifiable — no misrepresenting Marten, EventStoreDB, or custom implementation capabilities. Links to competitor documentation provided respectfully.

11. **AC11 - DAPR Explanation at Architectural Depth**: DAPR is explained at architectural depth per the progressive explanation pattern for concepts pages — which building blocks are used and why, linked to DAPR docs for further reading

12. **AC12 - Search Engine Optimization (NFR25)**: H1 title + one-paragraph summary with key terms ("event sourcing", ".NET", "comparison", "Marten", "EventStoreDB") in first 200 words

## Tasks / Subtasks

- [x] Task 1: Replace placeholder with page structure (AC: 5, 7, 9)
  - [x] Keep existing back-link to README: `[← Back to Hexalith.EventStore](../../README.md)`
  - [x] Keep H1 title: `# Choose the Right Tool`
  - [x] Replace placeholder paragraph with one-paragraph summary optimized for search (AC12): describe the page as a decision aid comparing Hexalith with Marten, EventStoreDB, and custom implementations
  - [x] Ensure heading hierarchy: H1 → H2 → H3, no skipped levels
  - [x] No YAML frontmatter

- [x] Task 2: Add context introduction section (AC: 6, 11)
  - [x] Add H2 section: `## What Is Hexalith.EventStore?`
  - [x] Brief 2-3 sentence introduction: DAPR-native event sourcing server for .NET, pure function contract model, infrastructure portability, built-in multi-tenant isolation
  - [x] Explain DAPR at architectural depth: "Hexalith uses DAPR building blocks — [state management](https://docs.dapr.io/developing-applications/building-blocks/state-management/), [pub/sub](https://docs.dapr.io/developing-applications/building-blocks/pubsub/), and [actors](https://docs.dapr.io/developing-applications/building-blocks/actors/) — to abstract infrastructure. This means you can swap Redis for PostgreSQL for Cosmos DB by changing a YAML configuration file, not your code."
  - [x] This section ensures self-containment for search engine arrivals

- [x] Task 3: Add comparison table section (AC: 1, 10)
  - [x] Add H2 section: `## Comparison at a Glance`
  - [x] Create markdown table comparing across these dimensions (deeper than README table):
    - Type (library vs server vs framework)
    - License (MIT vs MIT vs KLv1 source-available)
    - .NET support versions
    - Infrastructure portability
    - Multi-tenant isolation
    - CQRS framework completeness
    - Projection system
    - LINQ querying
    - Pub/sub
    - Deployment model
    - Database lock-in
    - Polyglot SDK support
    - Community maturity
    - Operational complexity
  - [x] Use accurate data: Marten 8.x (MIT, PostgreSQL only), KurrentDB 26.x (KLv1, dedicated server), Custom (varies)
  - [x] Link to competitor documentation respectfully

- [x] Task 4: Add detailed competitor sections (AC: 1, 10, 4)
  - [x] Add H2 section: `## Detailed Comparisons`
  - [x] Add H3: `### Hexalith vs Marten (Critter Stack)` — When Marten is better (PostgreSQL teams, LINQ needs, mature ecosystem), when Hexalith is better (infrastructure portability, multi-tenant, no PG dependency)
  - [x] Add H3: `### Hexalith vs EventStoreDB (KurrentDB)` — When EventStoreDB is better (max performance, polyglot, enterprise support, mature), when Hexalith is better (no dedicated server, infrastructure portability, multi-tenant, MIT license, complete CQRS framework)
  - [x] Add H3: `### Hexalith vs Custom Implementation` — When custom is better (minimal needs, full control, no dependencies), when Hexalith is better (production features built-in, multi-tenant, infrastructure portability, maintenance burden)
  - [x] Every limitation paired with a corresponding strength (AC4)

- [x] Task 5: Add "When Hexalith is NOT the Right Choice" section (AC: 3, 4)
  - [x] Add H2 section: `## When Hexalith Is Not the Right Choice`
  - [x] List specific scenarios with honest explanations and recommended alternatives:
    - Non-.NET stacks → EventStoreDB/KurrentDB (polyglot), Axon Framework (JVM)
    - Sub-millisecond latency → EventStoreDB/KurrentDB (no sidecar hop)
    - No container orchestration → Marten (library, no DAPR needed)
    - Already all-in on PostgreSQL → Marten (zero new infrastructure)
    - Need LINQ querying over events → Marten (full LINQ support)
    - Need polyglot SDKs → EventStoreDB/KurrentDB (6 languages)
    - Team unwilling to adopt DAPR → Marten or EventStoreDB
    - Need battle-tested production maturity today → Marten or EventStoreDB (Hexalith is pre-release)
  - [x] Each limitation paired with Hexalith's corresponding strength

- [x] Task 6: Add decision aid section (AC: 2)
  - [x] Add H2 section: `## Decision Guide`
  - [x] Create a numbered question sequence that guides the developer through decision points:
    1. Are you building with .NET? (No → EventStoreDB or custom)
    2. Do you need infrastructure portability? (Yes → Hexalith)
    3. Do you need built-in multi-tenant isolation? (Yes → Hexalith)
    4. Are you already running PostgreSQL and want zero new infrastructure? (Yes → Marten)
    5. Do you need maximum raw event stream performance? (Yes → EventStoreDB)
    6. Do you need polyglot SDK support? (Yes → EventStoreDB)
    7. Do you need LINQ querying against events? (Yes → Marten)
    8. Is this a simple proof of concept? (Yes → Custom/DIY)
    9. Default recommendation based on remaining criteria
  - [x] Format as a scannable markdown list, not a Mermaid diagram (too complex for this many paths)

- [x] Task 7: Add DAPR trade-offs summary section (AC: 11)
  - [x] Add H2 section: `## The DAPR Trade-Off`
  - [x] Explain at architectural depth: what DAPR building blocks Hexalith uses (state management, pub/sub, actors), why (infrastructure portability), and what trade-offs it introduces (runtime dependency, sidecar network hop, learning curve, version coupling)
  - [x] Link to DAPR docs for each building block
  - [x] Note that a deeper FAQ will be available at `docs/guides/dapr-faq.md` (future)

- [x] Task 8: Update Next Steps footer (AC: 5, 8)
  - [x] Keep H2 section: `## Next Steps`
  - [x] Primary next: `[Quickstart Guide](../getting-started/quickstart.md)` — "Ready to try Hexalith? Get running in under 10 minutes"
  - [x] Related: `[Prerequisites](../getting-started/prerequisites.md)`, `[README](../../README.md)`, `[Architecture Overview](architecture-overview.md)`
  - [x] All links use relative paths

- [x] Task 9: Final validation (AC: 5, 7, 8, 9, 10)
  - [x] Verify back-link to README uses correct relative path: `../../README.md`
  - [x] Verify heading hierarchy: H1 → H2 → H3, no skipped levels
  - [x] Verify no YAML frontmatter
  - [x] Verify all internal links use relative paths
  - [x] Verify no `[!NOTE]` alerts (use `> **Note:**` instead)
  - [x] Verify page is self-contained — reads coherently without prior pages
  - [x] Verify competitor information is factually accurate
  - [x] Verify every Hexalith limitation is paired with a strength
  - [x] Verify DAPR explanation at architectural depth with links to DAPR docs
  - [x] Verify key search terms in first 200 words

## Dev Notes

### Architecture Source

This story implements **FR3** (structured decision aid), **FR4** (competitor comparison), and **FR16** (when NOT to use Hexalith) from `_bmad-output/planning-artifacts/prd-documentation.md`, following the page structure defined in **Decision D7** from `_bmad-output/planning-artifacts/architecture-documentation.md`.

### Page Template (D7 — MANDATORY)

Every documentation page MUST follow this exact structure:

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

**For the choose-the-right-tool page specifically:** This page has NO prerequisites callout (it is a Hook-stage page for evaluators who may have seen nothing else). Omit the prerequisites blockquote section entirely.

### Key Technical Decisions

**Competitor Data (verified Feb 2026):**

| Competitor | Version | License | .NET Support | Key Differentiator |
|-----------|---------|---------|-------------|-------------------|
| Marten (Critter Stack) | 8.22.1 | MIT | .NET 8, .NET 9 | PostgreSQL-based document store + event store combo, full LINQ support |
| EventStoreDB (KurrentDB) | KurrentDB 26.0.1 | KLv1 (source-available, not OSI) | .NET 8, .NET 9, .NET Fx 4.8 | Purpose-built event database, polyglot SDKs (6 languages), built-in clustering |
| Custom/DIY | N/A | N/A | Any | Zero dependencies, full control, highest initial simplicity |

> **Note on EventStoreDB rebranding:** EventStoreDB was rebranded to KurrentDB in late 2024/early 2025. The page should use "EventStoreDB (now KurrentDB)" on first reference and "EventStoreDB" thereafter, since most developers still search for the original name. The .NET client package transitioned from `EventStore.Client.Grpc.*` to `KurrentDB.Client`.

**Hexalith Positioning (4 unique pillars):**

1. **DAPR-based infrastructure portability** — swap Redis for PostgreSQL for Cosmos DB with zero code changes
2. **Pure function contract model** — `(Command, CurrentState?) → List<DomainEvent>` with typed DomainResult (Success/Rejection/NoOp)
3. **Built-in multi-tenant isolation** — 4-layer model: input validation, composite key prefixing, DAPR actor scoping, JWT tenant enforcement
4. **Actor-based processing** — DAPR actors with turn-based concurrency, persist-then-publish pattern

**Innovation #2 — Radical Transparency (MANDATORY framing):**

Every stated Hexalith limitation MUST be paired with a corresponding strength. Frame as informed decision-making, not self-deprecation. Example structure:

> **If you need X, consider Y instead.** Hexalith trades X for Z — which matters when [use case].

Risk mitigation from PRD: "Choose the right tool" could be misread as lack of confidence → pair every limitation with a strength and frame as helping developers make the right decision.

**Comparison framing guidelines (from PRD):**
- Factually accurate and verifiable — no misrepresenting competitor capabilities
- Educational tone — link to competitor docs respectfully, focus on trade-offs not winners/losers
- "When NOT to use this" based on genuine technical limitations, not marketing spin
- No performance claims without reproducible benchmarks or explicit "target" labels

**DAPR Explanation Depth for Concepts Pages:**

| Page Type | DAPR Explanation Depth |
|-----------|----------------------|
| README | One sentence: "Built on DAPR for infrastructure portability" |
| Prerequisites | Installation-focused: what DAPR is, how to install it |
| Quickstart | Functional: "DAPR handles message delivery and state storage" |
| **Concepts (this page)** | **Architectural: which DAPR building blocks are used and why, linked to DAPR docs** |
| DAPR FAQ | Deep: honest trade-off analysis, risk assessment, what-if scenarios |

The choose-the-right-tool page should explain DAPR at architectural depth but remain accessible to developers who do NOT know DAPR (per voice/tone: assume reader knows .NET but NOT DAPR).

### Content Voice and Tone

- **Second person**: "you", "your" — "If you need infrastructure portability..."
- **Professional-casual**: Developer-to-developer, not marketing or academic
- **Active voice**: "Hexalith uses DAPR actors" not "DAPR actors are used by Hexalith"
- **Assume reader knows .NET** but NOT DAPR or Aspire
- **No emojis** in the documentation page itself
- **Callouts**: Use `> **Note:**` or `> **Tip:**` — NEVER use `[!NOTE]` GitHub alerts
- **Competitor respect**: Link to competitor docs, use accurate data, no snark

### NFRs This Story Supports

- **NFR6**: Heading hierarchy H1-H4 with no skipped levels
- **NFR8**: Color not sole indicator of meaning (in any tables/diagrams)
- **NFR9**: Code blocks with language-specific syntax highlighting tags (if any code blocks used)
- **NFR10**: Maximum 2 prerequisite page dependency (this page has zero prerequisites)
- **NFR11**: Self-contained markdown — no cross-file build dependencies
- **NFR25**: H1 title + one-paragraph summary with search-optimized terms
- **NFR26**: Descriptive filename (`choose-the-right-tool.md` — already exists)
- **NFR27**: 2-click depth from README (README → choose-the-right-tool = 1 click)

### FRs This Story Covers

- **FR3**: Developer can self-assess whether Hexalith fits their needs through a structured decision aid
- **FR4**: Developer can compare Hexalith's trade-offs against Marten, EventStoreDB, and custom implementations
- **FR16**: Developer can understand when Hexalith is NOT the right choice for their project
- **FR43**: Page is self-contained — developer arriving from search understands without reading other pages

### Cross-Linking Requirements (D7)

Choose-the-right-tool page links to:
- `../../README.md` — back-link at top of page
- `../getting-started/quickstart.md` — Next Step (for developers who decide to try Hexalith)
- `../getting-started/prerequisites.md` — Related link
- `architecture-overview.md` — Related link (same folder, relative link)
- External links: Marten docs (https://martendb.io/), KurrentDB docs (https://docs.kurrent.io/), DAPR docs (https://docs.dapr.io/)

Choose-the-right-tool page is linked FROM:
- `README.md` — "Choose the Right Tool" in Concepts section + "Note" callout in "Why Hexalith?" section
- `docs/getting-started/prerequisites.md` — Related link in Next Steps footer

### Anti-Patterns — What NOT to Do

| Anti-Pattern | Why It's Harmful |
|-------------|-----------------|
| Misrepresenting competitor capabilities | Destroys trust; verifiable claims only |
| Marketing language ("revolutionary", "best-in-class") | Evaluators see through it; use factual comparisons |
| Hiding Hexalith's limitations | Erodes trust; pair every limitation with a strength |
| Hard-coding competitor versions without context | Goes stale; use "8.x" not "8.22.1" in prose, put specific versions in a note |
| Using `[!NOTE]` GitHub-flavored alerts | Not portable; use `> **Note:**` blockquote instead |
| Adding YAML frontmatter | GitHub renders it as visible text |
| Complex Mermaid flowchart for decision aid | Too many paths → unreadable; use numbered question sequence instead |
| Treating DAPR as assumed knowledge | This is a Hook page; explain DAPR building blocks at architectural depth |
| Writing "click here" link text | Poor accessibility; use descriptive link text |
| Competitor bashing or snark | Unprofessional; frame as educational, link to competitor docs respectfully |
| Making performance claims without benchmarks | Unverifiable; label as "target" or omit |
| Assuming reader has read the README | FR43 requires self-contained pages |

### Project Structure Notes

**File to modify:**
- `docs/concepts/choose-the-right-tool.md` — replace placeholder with full decision aid content

**Files to reference (read-only):**
- `docs/page-template.md` — formatting conventions and page structure
- `README.md` — verify "Why Hexalith?" comparison table alignment (choose-the-right-tool should be a deeper expansion, not a contradiction)

**Alignment with project structure:**
- `docs/concepts/choose-the-right-tool.md` matches D1 folder structure exactly
- Back-link `../../README.md` is correct relative path from `docs/concepts/`
- Quickstart link `../getting-started/quickstart.md` is correct relative path
- Prerequisites link `../getting-started/prerequisites.md` is correct relative path
- Architecture overview link `architecture-overview.md` is same-folder relative link

### Previous Story Intelligence (8-3)

**Story 8-3 (Prerequisites & Local Dev Environment Page) — status: done:**
- `docs/getting-started/prerequisites.md` fully completed with all 12 acceptance criteria
- Page follows standard template: back-link, H1, summary, content sections, Next Steps footer
- Second person voice, professional-casual tone, no emojis
- All code blocks use `bash` language tag
- All internal links use relative paths
- No YAML frontmatter, no `[!NOTE]` alerts
- Self-contained per FR43 — explains WHY each tool is needed
- Cross-platform coverage (Windows, macOS, Linux)
- Story 8-3 Next Steps footer links to `choose-the-right-tool.md` as a Related link — confirming the link target

**What this means for Story 8-4:**
- Follow the exact same conventions demonstrated in 8-3: second person voice, professional-casual tone
- The choose-the-right-tool page is already linked from prerequisites.md Next Steps — it needs to exist and work
- The page should complement the README's comparison table with much deeper analysis
- Developers arriving from the README "Note" callout or the prerequisites "Related" link expect actionable decision guidance

### Git Intelligence

Recent commits show documentation initiative progress:
- `1450008` — Merge PR #62: Story 8-3 prerequisites page
- `225485f` — feat: Complete Story 8-3 Prerequisites & Local Dev Environment page
- `9270960` — feat: Complete Story 8-2 README rewrite with progressive disclosure (#61)
- `65eef1a` — feat: Update README and documentation structure for Hexalith.EventStore
- `2d4f3fb` — Merge PR #60: Story 8-1 implementation and 8-2 artifact

**Patterns observed:**
- Commit messages use conventional format: `feat:`, `chore:`, `fix:`
- PRs use branch naming: `feat/story-X-Y-description` or `chore/story-X-Y-description`
- Stories are implemented and reviewed in sequence
- Documentation pages are the primary deliverables — no code changes

### Latest Technical Information

**Competitor versions verified as of 2026-02-26:**

| Tool | Latest Stable | Key Details |
|------|--------------|-------------|
| Marten (JasperFx) | 8.22.1 | MIT license, .NET 8/9, PostgreSQL only, part of Critter Stack with Wolverine |
| EventStoreDB/KurrentDB | KurrentDB 26.0.1 (Feb 2026) | KLv1 license (source-available, not OSI), rebranded from EventStoreDB in late 2024 |
| KurrentDB .NET client | `KurrentDB.Client` 1.0.0 | .NET 8, .NET 9, .NET Fx 4.8. Legacy: `EventStore.Client.Grpc` v23.3.9 |
| DAPR | CLI 1.16.9, SDK 1.16.1 | Aligned with Hexalith project dependencies |

**Key competitive developments:**
- **Marten 9.0** expected Q2/Q3 2026 — continued innovation on Critter Stack
- **KurrentDB 26.0** (Dec 2025): Native Kafka source connector, relational sink to PostgreSQL/SQL Server, custom indices, archiving to AWS/Azure/GCP
- **KurrentDB rebranding**: Many developers still search for "EventStoreDB" — use both names on the page
- **Kurrent funding**: $12M raised December 2024 — signals continued investment

**Critical accuracy notes for the page:**
- Marten CANNOT switch away from PostgreSQL — this is a genuine lock-in, not FUD
- EventStoreDB's KLv1 license is source-available but NOT OSI-approved open source — enterprise features (LDAP, encryption at rest) require a paid license
- Hexalith is pre-release targeting .NET 10 — be honest about maturity gap vs Marten/EventStoreDB
- DAPR sidecar adds a network hop — do not claim it has zero performance overhead

### Testing Standards

This story produces a single markdown file (`docs/concepts/choose-the-right-tool.md`). Validation:

1. **Page template compliance**: Verify back-link, H1, summary, content sections, Next Steps footer
2. **Heading hierarchy check**: Verify H1 → H2 → H3 with no skipped levels
3. **No frontmatter**: Verify no YAML frontmatter at top of file
4. **Link check**: Verify all internal links use relative paths and point to existing files/placeholders
5. **Self-contained check**: Read the page in isolation — does it make sense without prior context?
6. **Competitor accuracy**: Cross-reference stated competitor features against official competitor docs
7. **Limitation-strength pairing**: Verify every Hexalith limitation is paired with a corresponding strength
8. **DAPR depth**: Verify DAPR is explained at architectural depth with links to DAPR docs
9. **No `[!NOTE]` alerts**: Verify only `> **Note:**` blockquote syntax is used
10. **Search terms**: Verify "event sourcing", ".NET", "comparison" appear in first 200 words

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-8.4] — Story definition with BDD acceptance criteria
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#D7] — Page template and cross-linking strategy
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#D1] — Content folder structure
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#D5] — Mermaid diagram conventions
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR3] — Structured decision aid requirement
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR4] — Competitor comparison requirement
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR16] — When NOT to use Hexalith requirement
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR43] — Self-contained page requirement
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#Innovation-2] — Radical Transparency framing
- [Source: docs/page-template.md] — Formatting conventions and page structure rules
- [Source: _bmad-output/implementation-artifacts/8-3-prerequisites-and-local-dev-environment-page.md] — Previous story conventions
- [Source: README.md#Why-Hexalith] — Existing comparison table to expand upon
- [Source: https://martendb.io/] — Marten official documentation
- [Source: https://docs.kurrent.io/] — KurrentDB (EventStoreDB) official documentation
- [Source: https://docs.dapr.io/] — DAPR official documentation

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

No issues encountered. Documentation-only story — no code compilation or test execution required.

### Completion Notes List

- Replaced placeholder content in `docs/concepts/choose-the-right-tool.md` with full decision aid page (177 lines)
- Page structure follows D7 page template: back-link, H1, one-paragraph SEO summary, content sections, Next Steps footer
- Added "What Is Hexalith.EventStore?" intro section for self-containment (FR43) with DAPR explained at architectural depth
- Created 14-dimension comparison table expanding on the README's 5-row table — covers type, license, .NET support, infrastructure portability, multi-tenancy, CQRS framework, projections, LINQ, pub/sub, deployment, DB lock-in, polyglot SDKs, maturity, operational complexity
- Added 3 detailed competitor comparison sections (Marten, EventStoreDB, Custom) with balanced "when X is better" / "when Hexalith is better" framing
- Added "When Hexalith Is Not the Right Choice" section covering all 8 scenarios from AC3, each with recommended alternative and "Hexalith trades X for Y" strength pairing (Innovation #2 Radical Transparency)
- Added 9-question Decision Guide as numbered question sequence (not Mermaid flowchart per anti-pattern guidance)
- Added "The DAPR Trade-Off" section with building block table, rationale, and 4 specific trade-offs (runtime dependency, sidecar hop, learning curve, version coupling)
- All competitor data factually accurate per verified Feb 2026 information: Marten 8.x MIT, KurrentDB 26.x KLv1, DAPR 1.16.x
- Used "EventStoreDB (now KurrentDB)" on first reference per rebranding guidance
- No YAML frontmatter, no `[!NOTE]` alerts, no emojis, all relative internal links
- Voice: second person, professional-casual, active voice, assumes .NET knowledge but not DAPR

### Change Log

- 2026-02-26: Implemented Story 8-4 — replaced placeholder with full "Choose the Right Tool" decision aid page covering competitor comparison, decision guide, DAPR trade-offs, and honest "when not to use Hexalith" guidance
- 2026-02-26: Senior developer review completed — fixed benchmark-style latency wording, corrected story reference anchor, synchronized file list with actual git changes, and marked story as done

### File List

- `docs/concepts/choose-the-right-tool.md` — modified (replaced placeholder with full decision aid content)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — modified (story status synchronized after review)
- `_bmad-output/implementation-artifacts/8-4-choose-the-right-tool-decision-aid.md` — modified (review notes, status, and metadata corrections)

## Senior Developer Review (AI)

### Outcome

Approve with fixes applied.

### Findings

- **HIGH:** Unverified quantitative performance statement in `docs/concepts/choose-the-right-tool.md` claimed "typically single-digit milliseconds" without benchmark evidence.
- **MEDIUM:** Story reference pointed to the wrong anchor (`Story-1.4`) instead of this story (`Story-8.4`).
- **MEDIUM:** Dev Agent Record `File List` did not reflect all files changed in git (`sprint-status.yaml` and this story file).

### Fixes Applied

- Replaced benchmark-like latency wording with non-quantified, evidence-safe wording.
- Corrected the `epics.md` reference anchor from `Story-1.4` to `Story-8.4`.
- Updated the story `File List` to match actual modified files.

