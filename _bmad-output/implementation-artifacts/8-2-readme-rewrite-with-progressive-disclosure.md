# Story 8.2: README Rewrite with Progressive Disclosure

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a .NET developer evaluating event sourcing solutions,
I want to land on the README and immediately understand what Hexalith.EventStore does, see the programming model, and compare it to alternatives,
So that I can decide within 30 seconds whether to invest more time.

## Acceptance Criteria

1. **AC1 - GIF Demo Placeholder**: The first viewport element is a placeholder for the animated GIF demo referencing `docs/assets/quickstart-demo.gif` (actual GIF is Story 8-5; use a static placeholder image or text placeholder now)

2. **AC2 - One-Liner + Badge Row**: Immediately after the GIF placeholder, a one-liner description ("DAPR-native event sourcing server for .NET") with a badge row showing: GitHub stars, NuGet version, build status (CI workflow), and MIT license

3. **AC3 - Hook Paragraph**: A hook paragraph that resonates with the target audience: "If you've spent weeks wiring up an event store, a message broker, and multi-tenant isolation — only to realize you'll do it again for your next project — we built this for you."

4. **AC4 - Pure Function Contract**: A single C# code block showing the core programming model: `(Command, CurrentState?) -> List<DomainEvent>` with the actual `IDomainProcessor` interface and a simplified Counter example

5. **AC5 - Comparison Table**: A "Why Hexalith?" comparison table covering Hexalith vs. Marten vs. EventStoreDB vs. custom implementations with rows for: infrastructure portability, multi-tenant isolation, CQRS/ES out of the box, deployment complexity, and database lock-in

6. **AC6 - Quickstart Link**: A prominent quickstart link ("Get started in under 10 minutes") above the fold, linking to `docs/getting-started/quickstart.md`

7. **AC7 - Architecture Diagram**: Below the fold, an inline Mermaid architecture diagram showing the system topology (EventStore server, DAPR sidecar, domain services, state store, pub/sub) with a `<details>` text description for accessibility (NFR7)

8. **AC8 - Documentation Links**: Documentation links organized by funnel stage (Getting Started, Concepts, Guides, Reference, Community)

9. **AC9 - Contributing + License Footer**: Contributing link to `CONTRIBUTING.md` and MIT license section

10. **AC10 - SEO Keywords**: The first 200 words contain ALL primary SEO keywords: event sourcing, .NET, DAPR, distributed, multi-tenant, event store, CQRS, DDD (NFR24)

11. **AC11 - Heading Hierarchy**: README uses structured heading hierarchy H1-H4 with no skipped levels (NFR6)

12. **AC12 - Code Block Language Tags**: All code blocks specify language tags (NFR9): `csharp`, `bash`, `yaml` etc.

13. **AC13 - No YAML Frontmatter**: README does NOT use YAML frontmatter (AC7 from Story 8-1)

14. **AC14 - Viewport Constraint**: Sections 1-6 (GIF, badges, hook, contract, comparison table, quickstart link) fit within the first viewport scroll — this is the 30-second evaluation window per architecture decision D6

## Tasks / Subtasks

- [ ] Task 1: Rewrite README.md header section (AC: 1, 2, 3, 10, 13)
  - [ ] Remove existing minimal README content
  - [ ] Add GIF demo placeholder at top (text placeholder referencing `docs/assets/quickstart-demo.gif`)
  - [ ] Add H1 title: `Hexalith.EventStore`
  - [ ] Add one-liner description
  - [ ] Add badge row (stars, NuGet, CI build, license)
  - [ ] Add hook paragraph with developer empathy narrative
  - [ ] Ensure first 200 words contain all 8 SEO keywords

- [ ] Task 2: Add pure function contract section (AC: 4, 12)
  - [ ] Add "The Programming Model" section (H2)
  - [ ] Show simplified `IDomainProcessor` interface in `csharp` code block
  - [ ] Show minimal Counter domain example in `csharp` code block
  - [ ] Brief explanation: "Your domain logic is a pure function. Hexalith handles everything else."

- [ ] Task 3: Add comparison table section (AC: 5)
  - [ ] Add "Why Hexalith?" section (H2)
  - [ ] Create comparison table: Hexalith vs Marten vs EventStoreDB vs Custom
  - [ ] Include rows: infrastructure portability, multi-tenant, CQRS/ES built-in, deployment, DB lock-in
  - [ ] Ensure honest positioning (FR16 — when Hexalith is NOT the right choice linked to future decision aid)

- [ ] Task 4: Add quickstart CTA (AC: 6, 14)
  - [ ] Add prominent quickstart link/section above the fold
  - [ ] Link to `docs/getting-started/quickstart.md` (placeholder page exists from 8-1)
  - [ ] Include "Prerequisites: .NET SDK, Docker Desktop, DAPR CLI" mention
  - [ ] Verify sections 1-6 fit in approximately one viewport scroll

- [ ] Task 5: Add architecture diagram section (AC: 7, 11)
  - [ ] Add "Architecture" section (H2)
  - [ ] Create inline Mermaid flowchart showing system topology
  - [ ] Include `<details>` block with text description for accessibility
  - [ ] One-sentence DAPR explanation: "Built on DAPR for infrastructure portability"

- [ ] Task 6: Add documentation links section (AC: 8)
  - [ ] Add "Documentation" section (H2)
  - [ ] Organize links by funnel stage: Getting Started, Concepts, Guides, Reference, Community
  - [ ] All links use relative paths to `docs/` subfolders
  - [ ] Include links to pages that exist (placeholders acceptable for future stories)

- [ ] Task 7: Add contributing and license footer (AC: 9)
  - [ ] Add "Contributing" section (H2) linking to `CONTRIBUTING.md`
  - [ ] Add "License" section (H2) with MIT license mention and link to `LICENSE`
  - [ ] Add CHANGELOG link per FR54

- [ ] Task 8: Final validation (AC: 10, 11, 12, 13, 14)
  - [ ] Verify heading hierarchy: H1 → H2 → H3 (no skipped levels)
  - [ ] Verify all code blocks have language tags
  - [ ] Verify no YAML frontmatter
  - [ ] Verify first 200 words contain all 8 SEO keywords
  - [ ] Verify relative links only (no absolute URLs for internal links)
  - [ ] Verify viewport constraint for above-the-fold content

## Dev Notes

### Architecture Source

This story implements **Decision D6: README Structure & Progressive Disclosure** from `_bmad-output/planning-artifacts/architecture-documentation.md`.

### README Section Order (D6 — MANDATORY)

The README MUST follow this exact section order per architecture decision D6:

1. **Animated GIF demo** — show, don't tell (FR5) — use placeholder for now
2. **One-liner description** + badge row (stars, NuGet, build status, license)
3. **The hook paragraph** — "If you've spent weeks wiring up an event store..." (FR1)
4. **Pure function contract** — single code block showing `(Command, CurrentState?) -> List<DomainEvent>` (FR2)
5. **Why Hexalith?** — comparison table vs. Marten, EventStoreDB, custom (FR4)
6. **Quickstart link** — prominent, above the fold (FR7)
7. **Architecture diagram** — Mermaid inline (FR11, parallel entry point for architects per FR45)
8. **Documentation links** — organized by funnel stage
9. **Contributing** — link to CONTRIBUTING.md
10. **License** — MIT

**CRITICAL: Sections 1-6 MUST fit in the first viewport scroll.** This is where the 30-second evaluation happens.

### Key Technical Decisions

**Badge Row Format:**

Use shields.io badges with the GitHub repository `Hexalith/Hexalith.EventStore`:

```markdown
[![GitHub stars](https://img.shields.io/github/stars/Hexalith/Hexalith.EventStore)](https://github.com/Hexalith/Hexalith.EventStore/stargazers)
[![NuGet](https://img.shields.io/nuget/v/Hexalith.EventStore.Contracts)](https://www.nuget.org/packages/Hexalith.EventStore.Contracts)
[![Build](https://img.shields.io/github/actions/workflow/status/Hexalith/Hexalith.EventStore/ci.yml?branch=main)](https://github.com/Hexalith/Hexalith.EventStore/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
```

**GIF Placeholder:**

Since the actual GIF is Story 8-5, use a text-based placeholder that communicates intent:

```markdown
<!-- TODO: Replace with animated GIF demo (Story 8-5) showing: clone → run → send command → see event -->
> **See it in action:** An animated demo of the quickstart will be added here showing the complete flow from clone to events flowing in the Aspire dashboard.
```

**Pure Function Contract Code Example:**

Use the actual `IDomainProcessor` interface from the codebase, simplified for README display:

```csharp
// Your entire domain logic is one pure function
public interface IDomainProcessor
{
    // (Command, CurrentState?) -> DomainResult
    Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState);
}

// Example: A complete Counter domain service
public class CounterProcessor : IDomainProcessor
{
    public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState)
    {
        return command.CommandType switch
        {
            "IncrementCounter" => Task.FromResult(
                DomainResult.Success(new[] { new CounterIncremented() })),
            "DecrementCounter" when GetCount(currentState) == 0 => Task.FromResult(
                DomainResult.Rejection(new[] { new CounterCannotGoNegative() })),
            "DecrementCounter" => Task.FromResult(
                DomainResult.Success(new[] { new CounterDecremented() })),
            _ => throw new InvalidOperationException($"Unknown: {command.CommandType}")
        };
    }
}
```

This is a simplified version of `samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs` for README readability.

**Comparison Table Content:**

| Feature | Hexalith.EventStore | Marten | EventStoreDB | Custom |
|---------|-------------------|--------|-------------|--------|
| Infrastructure portability | Any state store, any message broker (swap with zero code changes) | PostgreSQL only | Dedicated server only | Whatever you build |
| Multi-tenant isolation | Built-in per-tenant data path, topic, and access control | Manual implementation | Manual implementation | Whatever you build |
| CQRS/ES framework | Complete: commands, events, actors, snapshots, projections | Complete (PostgreSQL-coupled) | Event storage only (bring your own framework) | Whatever you build |
| Deployment | DAPR sidecar model — Docker Compose, Kubernetes, Azure Container Apps | Application library | Dedicated server + clients | Whatever you build |
| Database lock-in | None — Redis, PostgreSQL, Cosmos DB, etc. via DAPR components | PostgreSQL | EventStoreDB | Chosen database |

**DAPR Explanation Depth in README:**

Per architecture documentation, README should use ONE sentence only: "Built on DAPR for infrastructure portability." Do NOT explain DAPR internals. Deeper DAPR explanation belongs in quickstart and concept pages.

**Mermaid Architecture Diagram:**

Use a flowchart showing the core topology. Must include `<details>` for accessibility:

```mermaid
flowchart TB
    Client([Client Application]) -->|REST/gRPC| CommandAPI[Command API Gateway]
    CommandAPI -->|Route| Actor[Aggregate Actor]
    Actor -->|Invoke| Domain[Domain Service<br/>IDomainProcessor]
    Domain -->|Return events| Actor
    Actor -->|Persist| StateStore[(State Store<br/>Redis / PostgreSQL / Cosmos DB)]
    Actor -->|Publish| PubSub{{Pub/Sub<br/>RabbitMQ / Kafka / Azure Service Bus}}
    PubSub -->|Subscribe| Projections[Event Handlers / Projections]

    subgraph DAPR ["DAPR Sidecar (Infrastructure Abstraction)"]
        StateStore
        PubSub
    end
```

Followed by:
```markdown
<details>
<summary>Architecture diagram text description</summary>

The system follows a command-event architecture: Client applications send commands via REST/gRPC to the Command API Gateway, which routes them to Aggregate Actors. Each actor invokes the domain service (your IDomainProcessor implementation) and persists resulting events to a state store. Events are published to a pub/sub system for downstream consumers. DAPR provides the infrastructure abstraction layer, allowing you to swap state stores (Redis, PostgreSQL, Cosmos DB) and message brokers (RabbitMQ, Kafka, Azure Service Bus) without changing application code.

</details>
```

### SEO Keywords Checklist (NFR24)

All 8 keywords MUST appear in the first 200 words of the README:

- [ ] event sourcing
- [ ] .NET
- [ ] DAPR
- [ ] distributed
- [ ] multi-tenant
- [ ] event store
- [ ] CQRS
- [ ] DDD

Suggested approach: weave keywords naturally into the one-liner, hook paragraph, and programming model intro paragraph.

### NFRs This Story Supports

- **NFR6**: Heading hierarchy H1-H4 with no skipped levels
- **NFR7**: Mermaid diagram accessibility via `<details>` text description
- **NFR9**: All code blocks with language-specific syntax highlighting tags
- **NFR24**: SEO keywords in first 200 words
- **NFR25**: H1 title + one-paragraph summary
- **NFR27**: 2-click depth — README is the root; all docs pages within 2 clicks

### FRs This Story Covers

- **FR1**: Understand what Hexalith.EventStore does within 30 seconds
- **FR2**: See the core programming model within the first screen scroll
- **FR4**: Compare trade-offs against Marten, EventStoreDB, custom
- **FR5**: Visual demonstration placeholder (actual GIF is Story 8-5)
- **FR39**: Discoverable through GitHub search for key terms
- **FR45**: Architecture as parallel entry point directly from README
- **FR54**: Version reference in README linking to release tag / CHANGELOG

### Cross-Linking Requirements (D7)

README links to:
- `docs/getting-started/quickstart.md` — quickstart (placeholder exists from 8-1 `.gitkeep`)
- `docs/getting-started/prerequisites.md` — prerequisites (placeholder exists)
- `docs/concepts/choose-the-right-tool.md` — decision aid (placeholder exists)
- `docs/concepts/architecture-overview.md` — architecture deep dive (placeholder)
- `CONTRIBUTING.md` — contribution guidelines (exists from Story 8-1)
- `CHANGELOG.md` — changelog (exists from Story 8-1)
- `LICENSE` — MIT license (exists in repo root)

> **Note:** Some linked pages are placeholders (`.gitkeep` files). This is expected — those pages will be created in later stories. The README links establish the 2-click navigation structure now.

### DAPR Progressive Explanation Pattern

| Page Type | DAPR Explanation Depth |
|-----------|----------------------|
| **README** (this story) | One sentence: "Built on DAPR for infrastructure portability" |
| Quickstart | Functional: "DAPR handles message delivery and state storage — you don't write infrastructure code" |
| Concepts pages | Architectural: which DAPR building blocks are used and why |
| Deployment guides | Operational: full DAPR component configuration |
| DAPR FAQ | Deep: honest trade-off analysis |

### Anti-Patterns — What NOT to Do

| Anti-Pattern | Why It's Harmful |
|-------------|-----------------|
| Creating a new sample domain (e.g., "OrderProcessor") | Fragments the documentation; reader expects consistency with Counter sample |
| Using `[!NOTE]` GitHub-flavored alerts | Not portable to future docs site; use `> **Note:**` instead |
| Hard-coding version numbers in prose | Goes stale immediately; use "current release" or link to CHANGELOG |
| Explaining DAPR internals in the README | Violates progressive disclosure; README is "Hook", not "Understand" |
| Adding YAML frontmatter | GitHub renders it as visible text |
| Writing "click here" link text | Poor accessibility and SEO; use descriptive link text |
| Using absolute URLs for internal links | Must use relative paths per D7 cross-linking convention |
| Making the README a wall of text | Progressive disclosure means less is more above the fold |

### Project Structure Notes

**Files to modify:**
- `README.md` (root) — complete rewrite from current single-line content

**Files to reference (read-only):**
- `docs/page-template.md` — formatting conventions (Story 8-1 output)
- `samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs` — actual code for README example
- `src/Hexalith.EventStore.Client/Handlers/IDomainProcessor.cs` — actual interface
- `Directory.Build.props` — package description, repository URL, license info
- `.github/workflows/ci.yml` — CI workflow name for badge URL

**Alignment with project structure:**
- README.md is at repository root — standard GitHub convention
- All internal links use relative paths to `docs/`, `CONTRIBUTING.md`, `CHANGELOG.md`, `LICENSE`
- Badge URLs reference `Hexalith/Hexalith.EventStore` GitHub repo and NuGet packages

### Previous Story Intelligence (8-1)

**Story 8-1 (Documentation Folder Structure & Page Conventions) completed:**
- Created `docs/` folder structure with 6 subfolders + assets subdirectories
- Created `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `CHANGELOG.md` as root-level placeholders
- Created `docs/page-template.md` with all conventions
- All folders have `.gitkeep` files
- No YAML frontmatter rule established
- Kebab-case file naming convention established
- Relative links only convention established

**What this means for Story 8-2:**
- All target link destinations exist (as `.gitkeep` placeholders or actual files)
- Follow the page template conventions for markdown formatting
- The README is NOT a `docs/` page so it doesn't need a back-link, but it IS the root of the navigation tree
- CHANGELOG.md already exists — link to it from README

### Git Intelligence

Recent commits show Epic 7 completion (sample app, testing, CI/CD) and Epic 8 initialization:
- `207c6d3` — settings.json for permission configuration
- `f7f1d35` — Story 7.8 fixes and Epic 8 init
- `ec6bf5a` — Story 7.8 code review fixes and Story 8.1 artifact
- CI workflow exists at `.github/workflows/ci.yml` — use for badge URL
- Release workflow exists at `.github/workflows/release.yml`

### Testing Standards

This story produces a single markdown file (`README.md`). Validation:

1. **SEO keyword check**: Count first 200 words and verify all 8 keywords present
2. **Heading hierarchy check**: Verify H1 → H2 progression with no skipped levels
3. **Code block language check**: Verify every code fence has a language tag
4. **Link check**: Verify all internal links use relative paths and point to existing files/placeholders
5. **No frontmatter**: Verify no YAML frontmatter at top of file
6. **Mermaid rendering**: Preview on GitHub to verify Mermaid diagram renders correctly
7. **Badge rendering**: Preview on GitHub to verify badge images load
8. **Viewport test**: Open on GitHub and verify sections 1-6 fit in first scroll

### Actual Codebase References

**IDomainProcessor interface** — `src/Hexalith.EventStore.Client/Handlers/IDomainProcessor.cs`:
```csharp
public interface IDomainProcessor {
    Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState);
}
```

**Counter sample processor** — `samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs`:
- Handles `IncrementCounter`, `DecrementCounter`, `ResetCounter` commands
- Returns `DomainResult.Success()`, `DomainResult.Rejection()`, or `DomainResult.NoOp()`
- Pattern: switch on `command.CommandType`, return domain events

**NuGet packages** (for badge and documentation links):
- `Hexalith.EventStore.Contracts` — core types, command/event envelopes
- `Hexalith.EventStore.Client` — `IDomainProcessor`, registration extensions
- `Hexalith.EventStore.Testing` — in-memory test helpers
- `Hexalith.EventStore.Server` — event store server
- `Hexalith.EventStore.CommandApi` — REST API gateway
- `Hexalith.EventStore.Aspire` — .NET Aspire integration
- `Hexalith.EventStore.ServiceDefaults` — service defaults

**Repository URL**: `https://github.com/Hexalith/Hexalith.EventStore`
**License**: MIT (from `Directory.Build.props`)
**Package description**: "DAPR-native event sourcing server for .NET"

### References

- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#D6] - README structure & progressive disclosure
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#D5] - Mermaid diagram strategy
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#D7] - Cross-linking & navigation strategy
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR1-FR6] - Discovery & evaluation requirements
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR39-FR42] - SEO & discoverability
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR45] - Architecture parallel entry point
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#NFR6-NFR10] - Accessibility requirements
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#NFR24] - SEO keywords in first 200 words
- [Source: _bmad-output/planning-artifacts/epics.md#Story-1.2] - Story definition with BDD acceptance criteria
- [Source: _bmad-output/implementation-artifacts/8-1-documentation-folder-structure-and-page-conventions.md] - Previous story output
- [Source: src/Hexalith.EventStore.Client/Handlers/IDomainProcessor.cs] - Actual interface
- [Source: samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs] - Counter sample

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
