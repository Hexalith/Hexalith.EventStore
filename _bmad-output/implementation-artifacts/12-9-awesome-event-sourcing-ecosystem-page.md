# Story 12.9: Awesome Event Sourcing Ecosystem Page

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer exploring event sourcing resources,
I want a curated ecosystem page linking to related tools, libraries, and learning resources,
So that I can discover the broader ecosystem and evaluate Hexalith in context.

## Acceptance Criteria

1. Given a developer navigates to `docs/community/awesome-event-sourcing.md`, when they read the page, then the page lists curated resources organized by category: event sourcing frameworks (.NET and other), CQRS/DDD libraries, DAPR ecosystem, learning resources (articles, books, blogs), and complementary tools (message brokers, design, testing)
2. The page includes a "Where Hexalith.EventStore Fits" positioning section placed BEFORE the resource lists, with a link to Choose the Right Tool for detailed comparison (FR41)
3. The page follows the standard page template (back-link, H1, summary, Next Steps) — no prerequisites needed as this is an entry-point page
4. All external links use descriptive text (not "click here") (AC: #4)
5. The opening summary contains SEO keywords (event sourcing, .NET, DAPR, CQRS, DDD) within the first 100 words (NFR24, NFR25)
6. The page ends with a contribution invitation encouraging community PRs to update entries

## Tasks / Subtasks

- [x] Task 1: Create `docs/community/awesome-event-sourcing.md` with page template structure (AC: #3, #5)
    - [x] 1.1 Add back-link `[← Back to Hexalith.EventStore](../../README.md)` using Unicode arrow
    - [x] 1.2 Add H1 title "Awesome Event Sourcing"
    - [x] 1.3 Add one-paragraph summary containing SEO keywords (event sourcing, .NET, DAPR, CQRS, DDD) in the first 100 words. Content: curated list of event sourcing frameworks, CQRS/DDD libraries, DAPR ecosystem projects, message brokers, learning resources, and complementary tools for .NET developers. Audience: developers exploring event sourcing who want to discover the broader ecosystem and evaluate Hexalith.EventStore in context.
    - [x] 1.4 Add "New to event sourcing?" onboarding hook immediately after summary using blockquote tip format: `> **Tip:** New to event sourcing? Start with the [Quickstart](../getting-started/quickstart.md) to see the system running in minutes.`
    - [x] 1.5 No prerequisites needed (this is an entry-point page for discovery/SEO)
    - [x] 1.6 Add Next Steps footer:
        - "**Next:** [Architecture Overview](../concepts/architecture-overview.md) — understand the Hexalith.EventStore system topology"
        - "**Related:** [Choose the Right Tool](../concepts/choose-the-right-tool.md), [Quickstart](../getting-started/quickstart.md), [NuGet Packages Guide](../reference/nuget-packages.md)"

- [x] Task 2: Write "Where Hexalith.EventStore Fits" positioning section — MUST appear BEFORE resource lists (AC: #2)
    - [x] 2.1 Brief paragraph (3-4 sentences MAX) positioning Hexalith: a DAPR-native event sourcing server for .NET built on CQRS, DDD, and event sourcing patterns with .NET Aspire orchestration. Include link to [Hexalith.EventStore GitHub repo](https://github.com/Hexalith/Hexalith.EventStore) for search-landing developers.
    - [x] 2.2 Include Hexalith's sweet-spot one-liner for consistency with framework listings: "sweet spot: DAPR-native teams wanting infrastructure-abstracted event sourcing with zero vendor lock-in"
    - [x] 2.3 Key differentiators as compact bullet list (4 items max): DAPR sidecar architecture (infrastructure abstraction), multi-tenant at contract level, pure-function aggregate pattern, .NET Aspire local dev topology
    - [x] 2.4 Link to [Choose the Right Tool](../concepts/choose-the-right-tool.md) for detailed comparison with alternatives
    - [x] 2.5 Tone: generous to competitors, position as community participant not competitor-basher. This section must NOT read like marketing — keep it factual and brief.

- [x] Task 3: Write "Event Sourcing Frameworks (.NET)" section (AC: #1)
    - [x] 3.1 **Marten** — .NET transactional document DB and event store on PostgreSQL; sweet spot: PostgreSQL-native teams (https://martendb.io/, https://github.com/JasperFx/marten)
    - [x] 3.2 **KurrentDB** (formerly EventStoreDB) — purpose-built event-native database; sweet spot: dedicated event store infrastructure (https://www.kurrent.io/, https://github.com/kurrent-io/KurrentDB)
    - [x] 3.3 **Eventuous** — lightweight event sourcing library for .NET targeting KurrentDB; sweet spot: minimal-ceremony ES with KurrentDB (https://eventuous.dev/, https://github.com/Eventuous/eventuous)
    - [x] 3.4 **NEventStore** — persistence-agnostic event store for .NET; sweet spot: pluggable storage backends (https://github.com/NEventStore/NEventStore)
    - [x] 3.5 **EventFlow** — async/await CQRS+ES and DDD framework for .NET; sweet spot: highly configurable DDD framework (https://geteventflow.net/, https://github.com/eventflow/EventFlow)
    - [x] 3.6 Use bullet-list format (awesome-list convention) with name as bold link, one-line description, sweet-spot tag in parentheses. Do NOT use tables for resource listings.

- [x] Task 4: Write "CQRS & DDD Libraries" section (AC: #1)
    - [x] 4.1 **MediatR** — in-process mediator for commands, queries, and notifications. Note: v13+ uses dual licensing (RPL-1.5/commercial under Lucky Penny Software); evaluate free-tier eligibility (https://github.com/jbogard/MediatR)
    - [x] 4.2 **Wolverine** — .NET command bus and message broker from JasperFx; integrates natively with Marten for aggregate-handler CQRS/ES (https://wolverinefx.net/, https://github.com/JasperFx/wolverine)
    - [x] 4.3 **FluentValidation** — validation library for .NET with fluent API; commonly paired with CQRS command pipelines (https://docs.fluentvalidation.net/, https://github.com/FluentValidation/FluentValidation)

- [x] Task 5: Write "DAPR Ecosystem" section (AC: #1)
    - [x] 5.1 **Dapr** — CNCF-graduated distributed application runtime providing state, pub/sub, actors, and service invocation via sidecar (https://dapr.io/, https://github.com/dapr/dapr)
    - [x] 5.2 **Dapr .NET SDK** — official .NET SDK for Dapr (https://github.com/dapr/dotnet-sdk)
    - [x] 5.3 **CommunityToolkit.Aspire.Hosting.Dapr** — .NET Aspire Community Toolkit integration for DAPR sidecar support (https://github.com/CommunityToolkit/Aspire)

- [x] Task 6: Write "Learning Resources" section (AC: #1)
    - [x] 6.1 **Books** subsection:
        - "Domain-Driven Design" by Eric Evans (2003) — the "Blue Book"; foundational text for aggregates, bounded contexts, ubiquitous language
        - "Implementing Domain-Driven Design" by Vaughn Vernon (2013) — the "Red Book"; practical code-level guidance on DDD with CQRS and event sourcing
        - "Domain-Driven Design Distilled" by Vaughn Vernon (2016) — accessible intro for teams
        - "Versioning in an Event Sourced System" by Greg Young (Leanpub, free online at https://leanpub.com/esversioning/read) — definitive guide to event schema evolution
        - "Introducing EventStorming" by Alberto Brandolini (Leanpub) — the original EventStorming workshop technique book
    - [x] 6.2 **Articles** subsection:
        - Martin Fowler — "Event Sourcing" (https://martinfowler.com/eaaDev/EventSourcing.html) — foundational pattern definition
        - Martin Fowler — "CQRS" (https://martinfowler.com/bliki/CQRS.html) — authoritative CQRS definition
        - Martin Fowler — "What do you mean by 'Event-Driven'?" (https://martinfowler.com/articles/201701-event-driven.html) — disambiguates event notification, event-carried state transfer, event sourcing, and CQRS
        - Greg Young — CQRS Documents (https://cqrs.files.wordpress.com/2010/11/cqrs_documents.pdf) — the original comprehensive CQRS+ES document
    - [x] 6.3 **Blogs & Newsletters** subsection:
        - event-driven.io by Oskar Dudycz (https://event-driven.io/en/) — pragmatic, deeply technical articles on event sourcing in .NET
        - Architecture Weekly newsletter by Oskar Dudycz (https://www.architecture-weekly.com/) — weekly curated software architecture resources
    - [x] 6.4 **Reference Repositories** subsection:
        - EventSourcing.NetCore by Oskar Dudycz (https://github.com/oskardudycz/EventSourcing.NetCore) — comprehensive examples and self-paced workshops covering event sourcing in .NET

- [x] Task 7: Write "Complementary Tools" section (AC: #1)
    - [x] 7.1 **Message Brokers & Streaming** subsection — brief intro: "Event sourcing systems typically publish events through a message broker. These are commonly paired with the frameworks above."
        - Apache Kafka (https://kafka.apache.org/) — distributed event streaming platform; industry standard for high-throughput event pipelines
        - RabbitMQ (https://www.rabbitmq.com/) — open-source message broker; commonly used with DAPR pub/sub component
        - Azure Event Hubs (https://learn.microsoft.com/en-us/azure/event-hubs/) — managed event streaming service; Kafka-compatible, zero-infrastructure for Azure-native stacks
    - [x] 7.2 **Event Modeling & Design** subsection:
        - EventStorming (https://www.eventstorming.com/) — collaborative workshop technique for discovering domain events and processes
        - Event Modeling (https://eventmodeling.org/) — blueprint-style method for designing event-sourced information systems
    - [x] 7.3 **Testing** subsection:
        - Testcontainers for .NET (https://dotnet.testcontainers.org/) — throwaway Docker containers for integration tests (PostgreSQL, KurrentDB, Redis, etc.)
        - Verify (https://github.com/VerifyTests/Verify) — snapshot testing for .NET; useful for verifying event stream shapes and projection outputs
        - Bogus (https://github.com/bchavez/Bogus) — realistic fake data generator for .NET; useful for populating test aggregates
        - Respawn (https://github.com/jbogard/Respawn) — intelligent database cleanup for integration tests
    - [x] 7.4 **Cross-Ecosystem** subsection:
        - Axon Framework (JVM) (https://github.com/AxonFramework/AxonFramework) — dominant DDD/CQRS/ES framework on the JVM
        - Apache Pekko (https://pekko.apache.org/) — open-source fork of Akka with event sourcing support; JVM/Scala ecosystem
    - [x] 7.5 **Community Channels** subsection (optional, 2-3 entries max):
        - Dapr Discord (https://aka.ms/dapr-discord) — official Dapr community discussions
        - DDD-CQRS-ES Slack (https://ddd-cqrs-es.slack.com/) — community for DDD, CQRS, and event sourcing practitioners

- [x] Task 8: Write "Contributing to This Page" section — MUST appear as the LAST content section BEFORE Next Steps (AC: #6)
    - [x] 8.1 Brief invitation: "Know a resource we're missing? Contributions are welcome — [open a pull request](https://github.com/Hexalith/Hexalith.EventStore/pulls) to suggest additions."
    - [x] 8.2 Add maintenance note: "This page is reviewed quarterly to keep links current and add new projects."

- [x] Task 9: Update README.md cross-link (AC: #5)
    - [x] 9.1 Locate the community or documentation navigation section in `README.md`
    - [x] 9.2 Add entry: `- [Awesome Event Sourcing](docs/community/awesome-event-sourcing.md) — curated ecosystem resources`
    - [x] 9.3 If no community section exists in README, add a brief "Community" section with the link

- [x] Task 10: Verify page compliance and CI integration (AC: #3, #4)
    - [x] 10.1 No YAML frontmatter
    - [x] 10.2 All internal links use relative paths
    - [x] 10.3 All external links use descriptive text — not "click here" or bare URLs (AC: #4)
    - [x] 10.4 Second-person tone, present tense, professional-casual
    - [x] 10.5 One H1 per page
    - [x] 10.6 Back-link with Unicode `←`
    - [x] 10.7 No hard-wrap in markdown source
    - [x] 10.8 Bullet-list format for all resource listings (awesome-list convention, NOT tables)
    - [x] 10.9 Run `markdownlint-cli2 docs/community/awesome-event-sourcing.md` to verify lint compliance
    - [x] 10.10 Self-containment test: page understandable without any prerequisite reading
    - [x] 10.11 Verify lychee link checking covers this page's external links — check `.lycheeignore` and add entries if needed for known-problematic URLs (Leanpub free-read URL, Greg Young CQRS PDF). If a link breaks in CI, check Wayback Machine before removing the entry.

## Dev Notes

### Implementation Approach — New Community Page (MUST follow)

**This story creates `docs/community/awesome-event-sourcing.md` — a NEW file.** This is a curated ecosystem page, not a tutorial or reference page. It goes in `docs/community/` which currently contains only `.gitkeep`.

The page serves dual purposes:

1. **SEO hook** — attracts developers searching for event sourcing resources to discover Hexalith (FR41, NFR24)
2. **Community positioning** — establishes Hexalith as a generous community participant that links to alternatives

### Page Section Order (MUST follow)

Section order on the page MUST match task order. Tasks 1-8 map directly to page sections top-to-bottom:

1. Back-link + H1 + Summary + Onboarding hook (Task 1)
2. Where Hexalith.EventStore Fits (Task 2)
3. Event Sourcing Frameworks (.NET) (Task 3)
4. CQRS & DDD Libraries (Task 4)
5. DAPR Ecosystem (Task 5)
6. Learning Resources (Task 6)
7. Complementary Tools (Task 7)
8. Contributing to This Page (Task 8)
9. Next Steps (Task 1.6)

Do NOT reorder sections. The sequence follows the reader's journey: positioning first, then frameworks, then supporting libraries, then learning, then tools, then contribute, then navigate onward.

### Critical Task Priority

**Task 9 (README cross-link) is the highest-ROI task in this story.** Without it, the page is an island with no inbound links — it won't be discovered by readers or indexed by search engines. If implementation is cut short, Task 9 must NOT be skipped.

### Content Curation Guidelines (MUST follow)

- **Be generous to competitors.** Link to Marten, KurrentDB, Eventuous, etc. with respectful, accurate descriptions. Frame as "ecosystem peers," not competitors.
- **Position Hexalith within the list**, not above it. The page is about the ecosystem; Hexalith is part of it. The "Where Hexalith.EventStore Fits" section MUST appear before all resource lists.
- **Only include actively maintained projects** (or classic references for books/articles). Do NOT include abandoned or unmaintained projects.
- **Descriptive link text always** — never "click here" or bare URLs. Every link should describe what it points to.
- **Brief descriptions** — one sentence per entry, two at most. This is a curated list, not reviews.
- **Include one-liner sweet spots** — each framework entry should include a parenthetical sweet spot tag (e.g., "PostgreSQL-native," "dedicated event store," "DAPR-native") to help developers quickly identify which tool fits their scenario.
- **Organize by category** — frameworks, libraries, DAPR ecosystem, learning resources, complementary tools (message brokers, design, testing, cross-ecosystem)
- **Use bullet-list format** — this is an awesome-list page, NOT a reference table. Use bold name as link, one-line description, sweet-spot tag. Do NOT use tables for resource listings.
- **External links are expected** — this is the one page in the documentation that primarily links externally
- **Invite contributions** — end the page with a brief call-to-action for community PRs (per PRD: "community PRs welcome to update entries")

### Key Resources to Include (Verified as of March 2026)

**Event Sourcing Frameworks (.NET):**

- **[Marten](https://martendb.io/)** ([GitHub](https://github.com/JasperFx/marten)) — .NET transactional document DB and event store on PostgreSQL (sweet spot: PostgreSQL-native teams)
- **[KurrentDB](https://www.kurrent.io/)** ([GitHub](https://github.com/kurrent-io/KurrentDB)) — purpose-built event-native database, formerly EventStoreDB (sweet spot: dedicated event store infrastructure)
- **[Eventuous](https://eventuous.dev/)** ([GitHub](https://github.com/Eventuous/eventuous)) — lightweight event sourcing library for .NET targeting KurrentDB (sweet spot: minimal-ceremony ES with KurrentDB)
- **[NEventStore](https://github.com/NEventStore/NEventStore)** — persistence-agnostic event store for .NET (sweet spot: pluggable storage backends)
- **[EventFlow](https://geteventflow.net/)** ([GitHub](https://github.com/eventflow/EventFlow)) — async/await CQRS+ES and DDD framework for .NET (sweet spot: highly configurable DDD framework)

**CQRS/DDD Libraries:**

- **[MediatR](https://github.com/jbogard/MediatR)** — in-process mediator for commands, queries, and notifications. Note: v13+ uses dual licensing (RPL-1.5/commercial under Lucky Penny Software)
- **[Wolverine](https://wolverinefx.net/)** ([GitHub](https://github.com/JasperFx/wolverine)) — .NET command bus and message broker from JasperFx; integrates natively with Marten for aggregate-handler CQRS/ES
- **[FluentValidation](https://docs.fluentvalidation.net/)** ([GitHub](https://github.com/FluentValidation/FluentValidation)) — validation library for .NET; commonly paired with CQRS command pipelines

**DAPR Ecosystem:**

- **[Dapr](https://dapr.io/)** ([GitHub](https://github.com/dapr/dapr)) — CNCF-graduated distributed application runtime providing state, pub/sub, actors, and service invocation via sidecar
- **[Dapr .NET SDK](https://github.com/dapr/dotnet-sdk)** — official .NET SDK for Dapr
- **[CommunityToolkit.Aspire.Hosting.Dapr](https://github.com/CommunityToolkit/Aspire)** — .NET Aspire Community Toolkit integration for DAPR sidecar support

**Books (classics):**

- "Domain-Driven Design" by Eric Evans (2003) — the "Blue Book"; foundational text for aggregates, bounded contexts, ubiquitous language
- "Implementing Domain-Driven Design" by Vaughn Vernon (2013) — the "Red Book"; practical code-level guidance on DDD with CQRS and event sourcing
- "Domain-Driven Design Distilled" by Vaughn Vernon (2016) — accessible intro for teams
- "Versioning in an Event Sourced System" by Greg Young (free online at https://leanpub.com/esversioning/read) — definitive guide to event schema evolution
- "Introducing EventStorming" by Alberto Brandolini (Leanpub) — the original EventStorming workshop technique book

**Canonical Articles:**

- Martin Fowler — "Event Sourcing" (https://martinfowler.com/eaaDev/EventSourcing.html) — foundational pattern definition
- Martin Fowler — "CQRS" (https://martinfowler.com/bliki/CQRS.html) — authoritative CQRS definition
- Martin Fowler — "What do you mean by 'Event-Driven'?" (https://martinfowler.com/articles/201701-event-driven.html) — disambiguates event notification, event-carried state transfer, event sourcing, and CQRS
- Greg Young — CQRS Documents (https://cqrs.files.wordpress.com/2010/11/cqrs_documents.pdf) — the original comprehensive CQRS+ES document

**Blogs & Newsletters:**

- event-driven.io by Oskar Dudycz (https://event-driven.io/en/) — pragmatic, deeply technical articles on event sourcing in .NET
- Architecture Weekly newsletter by Oskar Dudycz (https://www.architecture-weekly.com/) — weekly curated software architecture resources

**Reference Repos:**

- EventSourcing.NetCore by Oskar Dudycz (https://github.com/oskardudycz/EventSourcing.NetCore) — comprehensive examples and self-paced workshops

**Design Techniques:**

- EventStorming (https://www.eventstorming.com/) — collaborative workshop technique for discovering domain events and processes
- Event Modeling (https://eventmodeling.org/) — blueprint-style method for designing event-sourced information systems

**Testing Tools:**

- Testcontainers for .NET (https://dotnet.testcontainers.org/) — throwaway Docker containers for integration tests
- Verify (https://github.com/VerifyTests/Verify) — snapshot testing for .NET; useful for verifying event stream shapes and projection outputs
- Bogus (https://github.com/bchavez/Bogus) — realistic fake data generator for .NET; useful for populating test aggregates
- Respawn (https://github.com/jbogard/Respawn) — intelligent database cleanup for integration tests

**Message Brokers & Streaming:**

- **[Apache Kafka](https://kafka.apache.org/)** — distributed event streaming platform; industry standard for high-throughput event pipelines
- **[RabbitMQ](https://www.rabbitmq.com/)** — open-source message broker; commonly used with DAPR pub/sub component
- **[Azure Event Hubs](https://learn.microsoft.com/en-us/azure/event-hubs/)** — managed event streaming service; Kafka-compatible, zero-infrastructure for Azure-native stacks

**Cross-Ecosystem:**

- Axon Framework (JVM) (https://github.com/AxonFramework/AxonFramework) — dominant DDD/CQRS/ES framework on the JVM
- Apache Pekko (https://pekko.apache.org/) — open-source fork of Akka with event sourcing support; JVM/Scala ecosystem

**Community Channels:**

- Dapr Discord (https://aka.ms/dapr-discord) — official Dapr community discussions
- DDD-CQRS-ES Slack (https://ddd-cqrs-es.slack.com/) — community for DDD, CQRS, and event sourcing practitioners

### Important Notes: Ecosystem Changes (2025-2026)

**KurrentDB Rebrand:** EventStoreDB rebranded to KurrentDB in 2025 (KurrentDB 25.0). Use "KurrentDB" as primary name with "(formerly EventStoreDB)" noted once for discoverability. GitHub org moved to https://github.com/kurrent-io. The .NET client SDK still lives at https://github.com/EventStore/EventStore-Client-Dotnet.

**MediatR License Change:** MediatR v13+ transferred to Lucky Penny Software with dual licensing (RPL-1.5/commercial). A free Community edition exists for qualifying organizations — free for most open-source and small commercial projects. The page should note this briefly so developers can evaluate their eligibility. The original repo URL (https://github.com/jbogard/MediatR) redirects to the new location.

### Broken Link Fallback Strategy

This page has more external links than any other page in the documentation. When lychee CI reports a broken link:

1. Check if the URL moved — search for the resource's new location
2. Check the Wayback Machine (https://web.archive.org/) for an archived version
3. Only remove an entry if the resource is truly gone with no replacement
4. For books, prefer linking to the publisher/author's canonical URL over retailer links

### Stale Content Detection Integration

Story 11-4 implemented stale content detection for the documentation. Verify that the staleness detection configuration includes `docs/community/` files so this page is flagged for quarterly review automatically.

### Page Conventions (MUST follow)

From `docs/page-template.md`:

- Back-link: `[← Back to Hexalith.EventStore](../../README.md)` — use Unicode `←`
- One H1 per page
- No prerequisites needed for this page (it's an entry point)
- Code blocks with language tags (not expected for this page — it's primarily prose/links)
- No YAML frontmatter
- No hard-wrap in markdown source
- Relative links for internal navigation, full URLs for external resources
- Second-person tone, present tense
- Next Steps footer with "Next:" and "Related:" links

### Content Tone (MUST follow)

Community/curated list style — generous, inclusive, factual:

- **Descriptive but brief** — one sentence per resource, not reviews
- **Respectful to all projects** — no ranking, no "Hexalith is better than X"
- **Organized for scanning** — bullet-list format with bold name links, one-line descriptions (awesome-list convention)
- **Educational framing** — "if you need X, consider Y" not "Y is the best"
- **Link generously** — to official sites, GitHub repos, documentation
- **Sweet-spot tags** — each framework gets a parenthetical identifying its best-fit scenario

### What NOT to Do

- Do NOT bash competitors or position Hexalith as superior
- Do NOT include abandoned or unmaintained projects
- Do NOT use "click here" or bare URLs for link text
- Do NOT add YAML frontmatter
- Do NOT hard-wrap markdown source lines
- Do NOT duplicate content from other pages — cross-reference choose-the-right-tool for comparisons
- Do NOT include projects you can't verify are still active
- Do NOT turn this into a review page — keep descriptions factual and brief
- Do NOT include specific version numbers that will go stale quickly

### Relationship to Adjacent Stories

- **Story 12-8 (NuGet Packages Guide):** Previous story in epic. Reference page for Hexalith's own packages. Cross-link target from this page.
- **Story 8-4 (Choose the Right Tool):** Comparison/decision aid page. Cross-link for detailed Hexalith vs. alternatives analysis. Do NOT duplicate comparison content — link to it.
- **Story 12-1 (Architecture Overview):** Concept page. Cross-link as "Next" step for understanding Hexalith internals.
- **Story 8-2 (README Rewrite):** Entry point. This story includes Task 10 to add a cross-link FROM README to this page in the community section.
- **Story 11-1/11-2 (Docs CI Pipeline):** Lychee link checking and markdownlint already configured. This page's external links will be covered by CI. Check `.lycheeignore` for any needed additions (Leanpub, PDF links).

### Previous Story (12-8) Intelligence

**Patterns established in 12-1 through 12-8:**

- Second-person tone, present tense, professional-casual
- Self-containment with inline concept explanations
- No YAML frontmatter
- Unicode `←` in back-links
- Tables for structured data
- `docs/community/` folder for community pages (currently only `.gitkeep`)

**12-8 status:** ready-for-dev (story file created but not yet implemented). Creates `docs/reference/nuget-packages.md`.

### Git Intelligence

Recent commits:

- Epic 11 (docs CI pipeline) completed — `markdownlint-cli2` and lychee link checking available. External links on this page will be validated by CI. Check `.lycheeignore` for Leanpub and PDF URLs that may need allowlisting.
- Epic 16 (fluent client SDK) completed — all packages now use fluent API patterns
- Concept pages (12-1 through 12-5) done — cross-reference targets exist
- Story 12-6 (first domain service tutorial) in review
- Stories 12-7 and 12-8 ready-for-dev — reference pages not yet implemented

### Files to Create/Modify

- **Create:** `docs/community/awesome-event-sourcing.md` (new file in `docs/community/` folder)
- **Modify:** `README.md` — add cross-link to this page in community section (Task 10)
- **Possibly modify:** `.lycheeignore` — add entries for Leanpub free-read URL or Greg Young CQRS PDF if lychee rejects them

### Project Structure Notes

- File path: `docs/community/awesome-event-sourcing.md`
- The `docs/community/` folder exists but contains only `.gitkeep`
- Adjacent community pages: future `roadmap.md` (story 15-5)
- Concept pages to cross-reference: `docs/concepts/architecture-overview.md`, `docs/concepts/choose-the-right-tool.md`
- Getting started pages to cross-reference: `docs/getting-started/quickstart.md`
- Reference pages to cross-reference: `docs/reference/nuget-packages.md` (12-8, may not exist yet)

### References

- [Source: _bmad-output/planning-artifacts/epics.md, Story 5.9 — Awesome Event Sourcing Ecosystem Page]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md, FR41 — Ecosystem page discoverability]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md, NFR24 — SEO keywords in first 200 words]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md, NFR25 — H1 + summary per page]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md, Risk Mitigation — "Quarterly review cadence; community PRs welcome to update entries"]
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md, SEO & Discoverability section]
- [Source: docs/page-template.md — page structure rules]
- [Source: docs/concepts/choose-the-right-tool.md — comparison page cross-reference]
- [Source: docs/concepts/architecture-overview.md — architecture cross-reference]
- [Source: .lycheeignore — link checking exclusions for CI]
- [Source: README.md — community section cross-link target]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- markdownlint-cli2: 0 errors on `docs/community/awesome-event-sourcing.md`

### Completion Notes List

- Created `docs/community/awesome-event-sourcing.md` with 10 sections following exact story task order
- Page includes SEO keywords (event sourcing, .NET, DAPR, CQRS, DDD) in first 100 words of summary
- "Where Hexalith.EventStore Fits" positioning section placed before all resource lists with generous, factual tone
- All 5 .NET ES frameworks listed with sweet-spot tags: Marten, KurrentDB, Eventuous, NEventStore, EventFlow
- CQRS/DDD libraries: MediatR (with licensing note), Wolverine, FluentValidation
- DAPR ecosystem: Dapr runtime, .NET SDK, CommunityToolkit Aspire integration
- Learning resources: 5 books, 4 canonical articles, 2 blogs/newsletters, 1 reference repo
- Complementary tools: 3 message brokers, 2 design techniques, 4 testing tools, 2 cross-ecosystem, 2 community channels
- Contributing section with quarterly review commitment
- README.md updated with cross-link in Community section (highest-ROI task per Dev Notes)
- `.lycheeignore` updated with 4 new entries for known-problematic URLs (Leanpub, WordPress PDF, Slack, aka.ms)
- All page conventions verified: no frontmatter, one H1, Unicode back-link, relative internal links, descriptive link text, bullet-list format, no hard-wrap
- Senior review auto-fixes applied: framework category now explicitly covers both .NET and other ecosystems under a single section; cross-ecosystem entries moved accordingly
- Senior review auto-fixes applied: broad host-level lychee suppressions tightened to exact URL suppressions for `cqrs_documents.pdf` and `aka.ms/dapr-discord`
- Senior review auto-fixes applied: added explicit `## Next Steps` heading to align with page-template structure

### Change Log

- 2026-03-01: Created `docs/community/awesome-event-sourcing.md` — curated ecosystem page with 40+ resources across 10 categories
- 2026-03-01: Added README.md cross-link to ecosystem page in Community section
- 2026-03-01: Updated `.lycheeignore` with entries for Leanpub, WordPress PDF, Slack, and aka.ms URLs
- 2026-03-01: Applied adversarial review fixes (AC #1 category completion, template heading alignment, URL-scoped link-ignore tightening), status set to `done`

### File List

- **Created:** `docs/community/awesome-event-sourcing.md`
- **Modified:** `README.md` (added ecosystem page link in Community section)
- **Modified:** `.lycheeignore` (URL-scoped suppressions for known-problematic external links)
- **Modified:** `_bmad-output/implementation-artifacts/12-9-awesome-event-sourcing-ecosystem-page.md` (review remediation + status sync)
- **Modified:** `_bmad-output/implementation-artifacts/sprint-status.yaml` (story state synchronization)

### Additional Workspace Git Changes Observed During Review (Out of Story Scope)

- `CONTRIBUTING.md` (modified)
- `_bmad-output/implementation-artifacts/11-3-documentation-validation-github-actions-workflow.md` (modified)
- `_bmad-output/implementation-artifacts/11-4-stale-content-detection.md` (modified)
- `docs/concepts/architecture-overview.md` (modified)
- `src/Hexalith.EventStore.CommandApi/Controllers/CommandStatusController.cs` (modified)
- `src/Hexalith.EventStore.CommandApi/Controllers/ReplayController.cs` (modified)
- `src/Hexalith.EventStore.CommandApi/Filters/ValidateModelFilter.cs` (modified)
- `.github/workflows/docs-validation.yml` (untracked)
- `_bmad-output/implementation-artifacts/12-1-architecture-overview-with-mermaid-topology.md` (untracked)
- `_bmad-output/implementation-artifacts/12-2-command-lifecycle-deep-dive.md` (untracked)
- `_bmad-output/implementation-artifacts/12-3-event-envelope-metadata-structure.md` (untracked)
- `_bmad-output/implementation-artifacts/12-4-identity-scheme-documentation.md` (untracked)
- `_bmad-output/implementation-artifacts/12-5-dapr-trade-offs-and-faq-intro.md` (untracked)
- `_bmad-output/implementation-artifacts/12-6-first-domain-service-tutorial.md` (untracked)
- `_bmad-output/implementation-artifacts/12-7-command-api-reference.md` (untracked)
- `_bmad-output/implementation-artifacts/12-8-nuget-packages-guide-and-dependency-graph.md` (untracked)
- `docs/concepts/command-lifecycle.md` (untracked)
- `docs/concepts/event-envelope.md` (untracked)
- `docs/concepts/identity-scheme.md` (untracked)
- `docs/getting-started/first-domain-service.md` (untracked)
- `docs/reference/command-api.md` (untracked)
- `docs/reference/nuget-packages.md` (untracked)
