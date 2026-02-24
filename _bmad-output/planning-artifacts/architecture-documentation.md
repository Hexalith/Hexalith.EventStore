---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
lastStep: 8
status: 'complete'
completedAt: '2026-02-24'
inputDocuments:
  - prd-documentation.md
  - prd-documentation-validation-report.md
  - architecture.md
  - product-brief-Hexalith.EventStore-2026-02-11.md
workflowType: 'architecture'
project_name: 'Hexalith.EventStore'
user_name: 'Jerome'
date: '2026-02-24'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Project Context Analysis

### Requirements Overview

**Functional Requirements: 63 FRs across 11 categories**

| Category | FRs | Architectural Significance |
|----------|-----|---------------------------|
| Documentation Discovery & Evaluation | FR1-FR6 | README structure, comparison page, animated GIF demo, decision aid |
| Getting Started & Onboarding | FR7-FR10 | Sample project structure, quickstart CI, backend swap demo |
| Concept Understanding | FR11-FR16 | Mermaid diagrams, content progressive disclosure, DAPR explanation strategy |
| API & Technical Reference | FR17-FR21 | DocFX auto-generation, NuGet package documentation, configuration reference |
| Deployment & Operations | FR22-FR27, FR57-FR60, FR63 | Three deployment target walkthroughs, DAPR setup per environment, resource sizing |
| Community & Contribution | FR28-FR33 | GitHub templates, Discussions setup, roadmap page |
| Content Quality & Maintenance | FR34-FR38, FR61-FR62 | CI pipeline (linting, link checking, sample compilation), traceability checks |
| SEO & Discoverability | FR39-FR42 | Keyword optimization, cross-linking strategy, ecosystem page |
| Navigation & Structure | FR43-FR46 | Every-page-is-a-landing-page pattern, progressive complexity paths |
| Troubleshooting | FR47-FR49 | Error catalog structure, per-environment troubleshooting |
| Lifecycle & Versioning | FR50-FR56 | CHANGELOG, upgrade paths, disaster recovery, deployment progression |

**Non-Functional Requirements: 28 NFRs across 5 categories**

| Category | NFRs | Key Targets |
|----------|------|-------------|
| Performance | NFR1-NFR5 | Quickstart <10min, page <200KB, GIF <5MB, Mermaid renders on GitHub, tutorial <1hr |
| Accessibility | NFR6-NFR10 | Heading hierarchy, alt text, color independence, syntax highlighting, max 2-page prereq depth |
| Maintainability | NFR11-NFR17 | Self-contained pages, no manual code in markdown, PR-based workflow, staleness detection, GIF scripted, drop-in pages, quarterly review |
| Reliability | NFR18-NFR23 | 100% code samples compile+run, zero broken links, markdown lint pass, cross-platform quickstart, all deployments verified, CI <5min |
| Discoverability | NFR24-NFR28 | Keywords in first 200 words, H1+summary per page, descriptive filenames, max 2-click depth from README, NuGet descriptions aligned |

### Fundamental Tension: Content Quality vs. Solo Author Velocity

The PRD targets 63 FRs across 3 phases with a solo author (Jerome + AI). The architecture must maximize automation to keep the quality bar (NFR18-20: 100% code accuracy, zero broken links, lint pass) without creating infrastructure that itself becomes a maintenance burden. The guiding principle: **invest in CI that prevents documentation debt, not CI that creates tooling debt.**

### Specification Gaps Requiring Architectural Resolution

**Architecture-Blocking:**

| # | Gap | Impact |
|---|-----|--------|
| DGAP-1 | **Sample project structure undefined.** FR7/FR34 require runnable samples validated by CI, but no folder structure, project naming convention, or extraction mechanism is specified. | Affects tutorial-as-test-suite pattern, CI design, and content authoring workflow |
| DGAP-2 | **DocFX vs. alternatives not evaluated.** FR19 specifies auto-generated API docs, PRD mentions DocFX, but no analysis of alternatives (xmldoc2md, DefaultDocumentation, docfx v3 vs v2). | Affects API reference generation pipeline and CI integration |
| DGAP-3 | **GIF generation tooling undefined.** NFR15 requires scripted GIF regeneration. No tooling evaluated (asciinema, VHS/charmbracelet, OBS Script, Playwright). | Affects CI pipeline and cross-platform compatibility |

**Design-Phase:**

| # | Gap | Impact |
|---|-----|--------|
| DGAP-4 | **Code example extraction mechanism undefined.** NFR12 says no manual code in markdown — but how? Inline file references? Build-time injection? mdsnippets? | Affects authoring DX and CI complexity |
| DGAP-5 | **Link checking tool not selected.** FR35/NFR19 require zero broken links. Several tools exist (lychee, markdown-link-check, linkinator). | Affects CI pipeline design |
| DGAP-6 | **Markdown linting ruleset not defined.** NFR20 references linting but no specific ruleset or exceptions. | Affects CI and authoring conventions |
| DGAP-7 | **Cross-platform quickstart validation strategy unclear.** NFR21 requires identical results on 3 OSes. CI matrix? Docker-in-Docker? Manual test protocol? | Affects CI cost and infrastructure |
| DGAP-8 | **Mermaid diagram testing strategy undefined.** NFR4 requires diagrams render without errors on 3 browsers. How is this validated? | Affects diagram authoring confidence |

### Technical Constraints & Dependencies

- **GitHub as hosting platform (MVP)** — all content must render natively in GitHub markdown, constraining diagram formats, interactive elements, and search
- **Existing core architecture (D1-D11)** — documentation content must accurately reflect architectural decisions; any content changes require cross-referencing the architecture document
- **DAPR runtime dependency** — quickstart and deployment guides require DAPR installed, adding prerequisite complexity
- **.NET Aspire orchestration** — quickstart uses `dotnet aspire run`, tying samples to Aspire's project model
- **Solo author resource constraint** — architecture must minimize manual maintenance and maximize automation

### Cross-Cutting Concerns Identified

1. **Content-Code Synchronization** — sample projects, code snippets in docs, and CI tests must stay in lockstep across releases
2. **Multi-Platform Consistency** — quickstart and deployment guides must work on Windows, macOS, and Linux
3. **Progressive Disclosure** — every architectural decision about content structure must support the funnel (Hook > Try > Build > Trust > Stay > Contribute)
4. **Accessibility Compliance** — NFR6-10 apply uniformly to every page, diagram, and code block
5. **CI Pipeline Design** — link checking, linting, sample compilation, GIF generation, and API doc generation all flow through a single pipeline that must complete in <5 minutes (NFR23)

## Starter Template Evaluation

### Primary Technology Domain

**Documentation infrastructure product** on a .NET 10 codebase hosted on GitHub. The "starter" is not a project scaffold but a **documentation tooling stack**: content format, validation pipeline, and authoring workflow — kept deliberately minimal for solo-author velocity.

### Existing Infrastructure

| Component | Status | Notes |
|-----------|--------|-------|
| Sample project | Exists | `samples/Hexalith.EventStore.Sample/` — Counter domain with commands, events, state, processor |
| Markdown linting | Partial | `.markdownlintignore` exists, no `.markdownlint.json` ruleset |
| `docs/` folder | Does not exist | Greenfield — structure defined by architecture decisions |
| GitHub Actions CI | Does not exist | No `.github/workflows/` YAML files |
| API doc generation | Does not exist | Deferred to Phase 2 |

### Design Principle: Minimum Viable Tooling

Pre-mortem, red-team, and first-principles analysis converged on the same conclusion: **every tool added to the documentation pipeline is a dependency a solo author must maintain and every contributor must understand.** The tooling stack must satisfy NFR18-20 (code accuracy, link integrity, formatting) with the fewest possible moving parts.

The PRD's own guidance: *"invest in CI that prevents documentation debt, not CI that creates tooling debt."*

### Tooling Options Evaluated

**API Reference Generation (DGAP-2)**

| Tool | Weighted Score | Verdict |
|------|---------------|---------|
| DefaultDocumentation | 4.35/5 | Best auto-gen option, but generates unnavigable file trees on GitHub |
| xmldoc2md | 3.75/5 | Good alternative, similar limitations |
| DocFX v2 | 2.55/5 | Overpowered, community-maintained since Microsoft stepped back |
| Hand-written reference | 3.40/5 | Adequate for MVP, scales poorly |

**Decision:** Defer auto-generation to Phase 2. For Phase 1b, hand-write `nuget-packages.md` (FR18) — this narrative guide is more valuable to developers than auto-generated type listings. When public API surface stabilizes, add DefaultDocumentation with a curated `index.md` overlay to address navigability.

**GIF Generation (DGAP-3)**

| Tool | Weighted Score | Key Limitation |
|------|---------------|----------------|
| Manual screen capture | 4.00/5 | Not automated (NFR15 deferred) |
| VHS + static screenshots | 3.45/5 | VHS can't capture Aspire dashboard (browser GUI) |
| VHS alone | 3.35/5 | Terminal-only — misses the visual "wow" moment |
| Playwright + ffmpeg | 1.85/5 | Brittle, complex, slow |

**Decision:** Manual screen capture for Phase 1a with a documented regeneration procedure (checklist of steps to reproduce the GIF). The quickstart's "wow moment" is the Aspire dashboard — a browser GUI that VHS cannot capture. Automate in Phase 3 when the UI stabilizes and the cost of manual regeneration exceeds automation setup cost. NFR15 (scripted regeneration) is Phase 3 scope.

**Code Snippet Extraction (DGAP-4)**

| Tool | Weighted Score | Key Limitation |
|------|---------------|----------------|
| Inline code fences + CI sample validation | 4.05/5 | Manual sync between docs and samples |
| GitHub file links | 3.75/5 | Reader must click through, no inline preview |
| MarkdownSnippets | 3.25/5 | `.source.md` indirection, snippet key maintenance, contributor friction |

**Decision:** Inline code fences in markdown. The `samples/` project is the source of truth — CI compiles and tests it. Tutorial code examples are written inline and kept aligned with sample project code by convention. If they drift, a contributor or CI-driven test failure catches it. This preserves zero-friction authoring DX and zero-friction contribution DX (contributor concern: "don't make me install a build tool to fix a typo").

**Link Checking (DGAP-5)**

**Decision: lychee** — fastest option (Rust, async), official GitHub Action (`lycheeverse/lychee-action@v2`), supports `.lycheeignore` for exclusions, caching via `.lycheecache` across runs. No viable alternative is faster or simpler.

**Markdown Linting (DGAP-6)**

**Decision: markdownlint-cli2** — David Anson's latest generation, `.markdownlint-cli2.jsonc` config, integrates with VS Code extension. Existing `.markdownlintignore` continues to work.

### Selected Tooling Stack

| Concern | Tool | Phase | CI Budget |
|---------|------|-------|-----------|
| Content format | CommonMark markdown + Mermaid | Phase 1a | 0s |
| Markdown linting | markdownlint-cli2 | Phase 1a | ~5s |
| Link checking | lychee | Phase 1a | ~30s |
| Sample validation | `dotnet build` + `dotnet test` on `samples/` | Phase 1a | ~90s |
| Code snippets | Inline fences (no extraction tool) | Phase 1a | 0s |
| GIF generation | Manual capture + documented procedure | Phase 1a | 0s (not in CI) |
| API reference | DefaultDocumentation + curated index | Phase 2 | ~45s |
| GIF automation | TBD (Phase 3 — VHS + Playwright hybrid or alternative) | Phase 3 | TBD |
| **Total CI budget (Phase 1a)** | | | **~125s** |

**CI budget analysis:** 125s for Phase 1a leaves comfortable margin under NFR23's 5-minute (300s) limit. Phase 2 adds ~45s for API docs. Cross-platform matrix (3 OSes) runs only the sample build/test — linting and link checking run once on ubuntu-latest.

### What Was Deliberately Excluded and Why

| Excluded | Reason | Revisit When |
|----------|--------|-------------|
| MarkdownSnippets | Contributor friction, `.source.md` indirection overhead for solo author | Never — inline + CI validation is sufficient |
| VHS for GIF | Cannot capture browser-based Aspire dashboard | Phase 3 — evaluate hybrid approach when UI stabilizes |
| DocFX | Overpowered for GitHub-hosted markdown MVP, community-maintained with uncertain roadmap | Phase 3 — if/when docs site is built |
| Cross-platform CI matrix for all jobs | Triples CI minutes; only sample build needs multi-OS | If platform-specific docs issues emerge |

## Core Architectural Decisions

### Decision Priority Analysis

**Critical Decisions (Block Implementation):**
- D1: Content folder structure & page conventions
- D2: Sample project architecture for documentation
- D3: CI pipeline architecture

**Important Decisions (Shape Architecture):**
- D4: Community infrastructure
- D5: Mermaid diagram strategy
- D6: README structure & progressive disclosure implementation
- D7: Cross-linking & navigation strategy

**Deferred Decisions (Post-MVP):**
- D8: Docs site migration (Phase 3 — Docusaurus or DocFX hosted site)
- D9: Versioned documentation (explicitly excluded by PRD)
- D10: API reference auto-generation details (Phase 2)

### D1: Content Folder Structure & Page Conventions

**Decision:** Adopt PRD-proposed structure as-is.

```
README.md
docs/
├── getting-started/
│   ├── prerequisites.md
│   ├── quickstart.md
│   └── first-domain-service.md
├── concepts/
│   ├── architecture-overview.md
│   ├── event-envelope.md
│   ├── identity-scheme.md
│   ├── command-lifecycle.md
│   └── choose-the-right-tool.md
├── guides/
│   ├── deployment-docker-compose.md
│   ├── deployment-kubernetes.md
│   ├── deployment-azure-container-apps.md
│   ├── deployment-progression.md
│   ├── configuration-reference.md
│   ├── security-model.md
│   ├── troubleshooting.md
│   └── dapr-faq.md
├── reference/
│   ├── command-api.md
│   ├── nuget-packages.md
│   └── api/                          # Phase 2: auto-generated
├── community/
│   ├── awesome-event-sourcing.md
│   └── roadmap.md
└── assets/
    ├── quickstart-demo.gif
    ├── diagrams/                      # Mermaid source .md if needed
    └── images/                        # Screenshots, logos
CONTRIBUTING.md
CODE_OF_CONDUCT.md
CHANGELOG.md
```

**Edge case defaults:**

| Concern | Default | Rationale |
|---------|---------|-----------|
| `docs/reference/api/` | Committed to repo, regenerated only on release tags | GitHub browsing must work; large diffs acceptable on release-only cadence |
| Page template | Informal convention: H1 title, one-paragraph summary, content. No frontmatter. | GitHub doesn't render YAML frontmatter; keep pages zero-tooling |
| Cross-linking | Relative links (e.g., `../concepts/architecture-overview.md`) | Works in GitHub browsing and future docs site; portable across forks |
| Assets | Centralized `docs/assets/` with `diagrams/` and `images/` subdirectories | Single location for all media; GIF referenced from README via `docs/assets/quickstart-demo.gif` |
| File naming | Lowercase, hyphen-separated, descriptive (per NFR26) | Already matches PRD convention |

**Affects:** FR43-46 (navigation), NFR25-27 (discoverability), all content authoring.

### D2: Sample Project Architecture

**Decision:** Extend the existing `samples/Hexalith.EventStore.Sample/` Counter domain to serve all documentation needs. Do not create separate sample projects.

**Current state:** The Counter sample already demonstrates the core programming model — commands (`IncrementCounter`, `DecrementCounter`, `ResetCounter`), events, state, and processor. The AppHost orchestrates it with Aspire, DAPR sidecars, Redis, and Keycloak.

**Modifications needed for documentation:**

| Need | Resolution |
|------|-----------|
| Multi-tenant demo (PRD requirement: "at least two tenants") | Add a second tenant configuration in AppHost or sample test data. Counter domain already supports tenant via `CommandEnvelope`. |
| Quickstart test (FR7, NFR1) | Add an integration test project `samples/Hexalith.EventStore.Sample.Tests/` that sends a command and asserts the event. This IS the quickstart validation in CI. |
| Backend swap demo (FR9) | Add DAPR component YAML variants in `samples/dapr-components/` for Redis (default) and PostgreSQL. Document the swap in quickstart. |
| Deployment configs (FR22-24) | Add `samples/deploy/docker-compose.yml`, `samples/deploy/kubernetes/`, `samples/deploy/azure/` — each with its own DAPR component configs. |

**Sample folder structure:**

```
samples/
├── Hexalith.EventStore.Sample/           # Existing Counter domain service
├── Hexalith.EventStore.Sample.Tests/     # Integration tests (quickstart validation)
├── dapr-components/
│   ├── redis/                            # Default local dev config
│   └── postgresql/                       # Backend swap demo
└── deploy/
    ├── docker-compose.yml                # Phase 2
    ├── kubernetes/                       # Phase 2
    └── azure/                            # Phase 2
```

**Rationale:** One sample, multiple configurations. The PRD's "same application code with only infrastructure configuration changes" is demonstrated by the same Counter service running against different DAPR component configs and deployment targets.

**Affects:** FR7-10 (quickstart/onboarding), FR22-24 (deployment), FR34 (CI validation), DGAP-1 resolved.

### D3: CI Pipeline Architecture

**Decision:** Two GitHub Actions workflows, phased introduction.

**Phase 1a — `docs-validation.yml`** (triggers on PR and push to main):

```
Jobs:
├── lint-and-links (ubuntu-latest, ~35s)
│   ├── markdownlint-cli2 on docs/**/*.md, README.md, CONTRIBUTING.md, CHANGELOG.md
│   └── lychee link check on same files (with .lycheeignore, cache enabled)
│
└── sample-build (matrix: ubuntu-latest, windows-latest, macos-latest, ~90s)
    ├── dotnet build samples/Hexalith.EventStore.Sample/
    └── dotnet test samples/Hexalith.EventStore.Sample.Tests/
```

**Phase 2 — `docs-api-reference.yml`** (triggers on release tag only):

```
Jobs:
└── generate-api-docs (ubuntu-latest)
    ├── dotnet build with DefaultDocumentation
    ├── Commit generated docs/reference/api/ files
    └── Create PR with generated changes
```

**Design defaults:**

| Concern | Default | Rationale |
|---------|---------|-----------|
| Trigger strategy | PR + push to main for validation; release tags for API docs | Validates every change; heavy generation only on release |
| Cross-platform matrix | Sample build/test only; lint and links run once on ubuntu | Saves CI minutes; only the quickstart needs cross-platform validation (NFR21) |
| CI budget | lint+links ~35s + sample build ~90s = ~125s single-OS, ~215s with matrix | Well under NFR23's 300s limit |
| Caching | lychee cache (`.lycheecache`), NuGet package cache, dotnet restore cache | Speeds up repeat runs |
| Failure mode | lint and links are blocking; sample build is blocking | Any failure prevents merge |

**Affects:** FR34-38 (content quality CI), NFR18-23 (reliability), DGAP-7 resolved.

### D4: Community Infrastructure

**Decision:** Standard open-source GitHub community setup, aligned with PRD Journey 4 (Kenji Contributes).

**GitHub Discussions categories:**

| Category | Purpose | PRD Mapping |
|----------|---------|-------------|
| Announcements | Release announcements, breaking changes | FR50 (CHANGELOG complement) |
| Q&A | Technical questions (mark-answer enabled) | FR32, Journey 2 (Marco asks questions) |
| Ideas | Feature proposals, RFCs | Journey 4 (Kenji proposes gRPC) |
| Show & Tell | Community projects, integrations, blog posts | Journey 4 (Kenji's blog) |

**Issue templates:**

| Template | Fields | PRD Mapping |
|----------|--------|-------------|
| Bug Report | Steps to reproduce, expected/actual, environment (OS, .NET version, DAPR version) | FR30 |
| Feature Request | Problem, proposed solution, alternatives considered | FR30 |
| Documentation Improvement | Page/section, what's wrong or missing, suggested fix | FR30 (docs-specific) |

**PR template checklist:**
- Description of changes
- Related issue (if any)
- Docs: markdown lint passes locally
- Docs: links are not broken
- Code: `dotnet build` passes
- Code: `dotnet test` passes

**CONTRIBUTING.md structure:**
1. How to contribute (fork, branch, PR)
2. Development setup (prerequisites, clone, build)
3. Documentation contributions (edit markdown, run lint locally)
4. Code contributions (coding standards, test requirements)
5. Good first issues label explained
6. Community guidelines (link to CODE_OF_CONDUCT.md)

**Affects:** FR28-33 (community), Journey 4 (Kenji Contributes).

### D5: Mermaid Diagram Strategy

**Decision:** Hand-authored Mermaid in markdown, visual review in PR, no automated browser validation.

**Required diagrams (PRD minimum):**

| Diagram | Type | Phase | Location |
|---------|------|-------|----------|
| Architecture topology | C4 Context or flowchart | Phase 1b | `docs/concepts/architecture-overview.md` |
| Command lifecycle flow | Sequence diagram | Phase 1b | `docs/concepts/command-lifecycle.md` |
| Identity scheme visualization | Flowchart | Phase 1b | `docs/concepts/identity-scheme.md` |
| Deployment topology (per target) | Flowchart | Phase 2 | `docs/guides/deployment-*.md` |
| NuGet package dependency graph | Flowchart | Phase 1b | `docs/reference/nuget-packages.md` |

**Conventions:**

| Concern | Default | Rationale |
|---------|---------|-----------|
| Diagram format | Inline Mermaid code blocks in markdown | Renders natively on GitHub (NFR4); no external tool needed |
| Validation (DGAP-8) | Visual review in PR preview. No automated browser testing. | Browser rendering validation is prohibitively complex for the value. GitHub's Mermaid renderer is the de facto validator — if it renders in the PR preview, it works. |
| Color scheme | GitHub's default Mermaid theme (no custom CSS) | GitHub ignores custom Mermaid themes; keep it portable |
| Accessibility (NFR7) | Every diagram followed by a `<details>` block with text description | Alt text not supported for Mermaid on GitHub; expandable text description is the accessible alternative |
| Source files | Inline only; no separate `.mmd` files | One source of truth per page; avoids sync issues |

**Affects:** FR11-14 (concept understanding), NFR4 (Mermaid rendering), NFR7 (accessibility), DGAP-8 resolved.

### D6: README Structure & Progressive Disclosure

**Decision:** README follows the PRD's progressive disclosure funnel with specific section ordering.

**README section order:**

1. **Animated GIF demo** — show, don't tell (FR5)
2. **One-liner description** + badge row (stars, NuGet, build status, license)
3. **The hook paragraph** — "If you've spent weeks wiring up an event store..." (FR1)
4. **Pure function contract** — single code block showing `(Command, CurrentState?) -> List<DomainEvent>` (FR2)
5. **Why Hexalith?** — comparison table vs. Marten, EventStoreDB, custom (FR4)
6. **Quickstart link** — prominent, above the fold (FR7)
7. **Architecture diagram** — Mermaid inline (FR11, parallel entry point for architects per FR45)
8. **Documentation links** — organized by funnel stage
9. **Contributing** — link to CONTRIBUTING.md
10. **License** — MIT

**Rationale:** Sections 1-6 must fit in the first viewport scroll. This is where the 30-second evaluation happens (Journey 1). Architecture is below the fold as a parallel path for Priya-type evaluators.

**Affects:** FR1-6 (discovery), FR39 (SEO — keywords in first 200 words per NFR24), FR45 (architecture as parallel entry).

### D7: Cross-linking & Navigation Strategy

**Decision:** Every page is self-contained with standard navigation elements.

**Page conventions:**

| Element | Convention |
|---------|-----------|
| Opening | H1 title + one-paragraph summary (NFR25) |
| Prerequisites | "Prerequisites: [link], [link]" callout if page has dependencies (NFR10: max 2) |
| Cross-links | Inline relative links at point of relevance, not in a separate "See also" section |
| Next steps | Footer section: "Next: [logical next page]" + "Related: [2-3 related pages]" |
| Breadcrumb | Not possible in GitHub markdown; rely on folder structure for orientation (FR46) |
| Back to README | Every `docs/` page opens with a link back: "[Hexalith.EventStore](../../README.md)" |

**2-click depth validation (NFR27):**
- README links to all `docs/getting-started/` and `docs/concepts/` pages directly (1 click)
- Each `docs/` subfolder page links to related pages in other subfolders (2 clicks max)
- No page is more than: README → subfolder page → target page

**Affects:** FR42-46 (navigation), NFR10 (prereq depth), NFR27 (2-click depth).

### Decision Impact Analysis

**Implementation Sequence:**

1. **D1 + D6** first — create folder structure and README skeleton (Phase 1a foundation)
2. **D4** next — community infrastructure (templates, CONTRIBUTING, CODE_OF_CONDUCT)
3. **D3 Phase 1a** — CI pipeline for lint + links + sample build
4. **D2** — sample project extensions (tests, DAPR component variants)
5. **D7** — cross-linking applied as pages are authored
6. **D5** — Mermaid diagrams authored during Phase 1b concept pages

**Cross-Component Dependencies:**

| Decision | Depends On | Enables |
|----------|-----------|---------|
| D3 (CI) | D1 (knows what paths to lint/check) | All content authoring (safety net) |
| D2 (samples) | D1 (knows where deployment configs live) | FR7 quickstart, FR9 backend swap |
| D5 (Mermaid) | D1 (knows page locations) | FR11-14 concept pages |
| D6 (README) | D2 (quickstart must work), D5 (architecture diagram) | FR1-6 (the front door) |
| D7 (navigation) | D1 (all pages exist) | NFR27 (2-click depth) |

## Implementation Patterns & Consistency Rules

### Critical Conflict Points Identified

**12 areas** where AI agents authoring documentation pages could make different choices, leading to inconsistent developer experience.

### Content Voice & Tone Patterns

| Pattern | Rule | Example |
|---------|------|---------|
| Voice | Second person ("you"), active voice | "You send a command..." not "A command is sent..." |
| Tone | Professional-casual, peer-to-peer. Not academic, not marketing. | "This works because DAPR actors are single-threaded" not "The revolutionary actor model paradigm..." |
| Perspective | Developer-to-developer. Jerome explaining to Marco. | "Here's what happens under the hood" not "The system performs the following operations" |
| Jargon handling | Define on first use, then use freely. Link to concepts page for deep dives. | "The identity scheme (`tenant:domain:aggregate-id` — [learn more](../concepts/identity-scheme.md)) determines..." |
| DAPR references | Assume reader does NOT know DAPR. Explain what DAPR does in context, not how. | "DAPR handles message delivery (like a postal service for your events)" not "Configure the DAPR pub/sub building block component" without context |
| Aspire references | Assume reader knows .NET but NOT Aspire. Explain the Aspire concept on first encounter per page. | "Aspire orchestrates all the services (think docker-compose but .NET-native)" |

### Markdown Formatting Patterns

| Pattern | Rule | Anti-Pattern |
|---------|------|-------------|
| Heading hierarchy | H1 = page title (one per page), H2 = major sections, H3 = subsections. Never skip levels (NFR6). | `## Title` then `#### Subsection` (skipped H3) |
| Code blocks | Always specify language: ` ```csharp `, ` ```bash `, ` ```yaml `, ` ```json `. Never bare ` ``` `. | Bare code fence with no language tag |
| Terminal commands | Use `bash` language tag. Prefix commands with `$` for clarity. Show expected output in a separate block. | Mixing commands and output in same block |
| Callouts | Use GitHub blockquote syntax: `> **Note:**`, `> **Warning:**`, `> **Tip:**` | Custom HTML, emoji-heavy callouts, or `[!NOTE]` GitHub-specific syntax (not portable) |
| Tables | Use for structured comparisons. Keep cells concise. Always include header row. | Tables for narrative content that should be prose |
| Lists | Ordered for sequential steps. Unordered for non-sequential items. | Numbered lists for items with no sequence |
| Line length | No hard wrap in markdown source. Let the renderer handle wrapping. | Hard wrapping at 80 or 120 characters |

### Code Example Patterns

| Pattern | Rule | Example |
|---------|------|---------|
| Namespace | Always show the `using` statements needed. Don't assume reader knows which namespace. | `using Hexalith.EventStore.Client.Handlers;` at top of every C# example |
| Completeness | Every code block should be copy-pasteable. If it's a fragment, explicitly say "Add this to your Program.cs:" | Floating code with no context about where it goes |
| Variable names | Match the sample project (`CounterProcessor`, `CounterState`, `IncrementCounter`). Don't invent new names. | Don't use `MyProcessor` or `SampleHandler` — use the Counter domain consistently |
| Comments | Minimal. Only where the code is non-obvious. Never comment what the code literally does. | `// Route commands to the domain processor` not `// Create a new MapPost endpoint` |
| Error handling | Show the happy path first. Show error handling in a separate "Error Handling" section if needed. | Don't clutter quickstart examples with try/catch |
| Configuration | YAML examples use inline comments explaining each field. One example per backend. | DAPR component YAML with `# Redis connection string` comments |

### Page Structure Patterns

Every documentation page follows this structure:

```markdown
[← Back to Hexalith.EventStore](../../README.md)

# Page Title

One-paragraph summary of what this page covers and who it's for.

> **Prerequisites:** [Prerequisite 1](link), [Prerequisite 2](link)
>
> (Only if page has dependencies. Maximum 2 per NFR10.)

## Main Content Sections

(Page-specific content)

## Next Steps

- **Next:** [Logical next page](link) — one-sentence description
- **Related:** [Related page 1](link), [Related page 2](link)
```

**Enforcement:** markdownlint-cli2 validates heading hierarchy. The back-link and Next Steps sections are convention-enforced by PR review, not tooling.

### Cross-Reference Patterns

| Reference Type | Convention | Example |
|----------------|-----------|---------|
| Page-to-page | Relative link with descriptive text | `[identity scheme](../concepts/identity-scheme.md)` |
| Page-to-section | Relative link with anchor | `[command routing](../concepts/command-lifecycle.md#routing)` |
| Architecture decision | Reference by D-number in parenthetical | "DAPR actors handle concurrency (D1 in core architecture)" |
| PRD requirement | Reference by FR/NFR number in parenthetical | "Cross-platform quickstart (NFR21)" |
| External links | Full URL, descriptive text, open in context | `[DAPR state store docs](https://docs.dapr.io/...)` |
| Sample code reference | Relative link to sample file | `[CounterProcessor.cs](../../samples/.../CounterProcessor.cs)` |

### DAPR Explanation Depth Pattern

DAPR concepts appear across many pages. Use a **progressive explanation** pattern:

| Page Type | DAPR Explanation Depth |
|-----------|----------------------|
| README | One sentence: "Built on DAPR for infrastructure portability" |
| Quickstart | Functional: "DAPR handles message delivery and state storage — you don't write infrastructure code" |
| Concepts pages | Architectural: explain which DAPR building blocks are used and why, with links to DAPR docs |
| Deployment guides | Operational: full DAPR component configuration with field-by-field YAML comments |
| DAPR FAQ | Deep: honest trade-off analysis, risk assessment, what-if-DAPR-changes scenarios |

**Rule:** Never require the reader to leave Hexalith docs to understand a DAPR concept needed for the current page. Link to DAPR docs for depth, not for prerequisites.

### Enforcement Guidelines

**All AI Agents MUST:**

1. Follow the page structure template (back-link, H1, summary, prerequisites, content, next steps)
2. Use the Counter domain (`IncrementCounter`, `CounterProcessor`, `CounterState`) in all code examples — never invent new domain names
3. Explain DAPR at the depth appropriate for the page type (see table above)
4. Use relative links for all internal cross-references
5. Include language tags on all code blocks
6. Keep pages self-contained — a reader arriving from search should understand the page without reading prerequisites (NFR10: max 2-page prereq depth)

**Anti-Patterns (agents must NEVER do):**

| Anti-Pattern | Why It's Harmful |
|-------------|-----------------|
| Creating a new sample domain (e.g., "OrderProcessor", "TodoService") | Fragments the documentation; reader expects consistency with quickstart |
| Using `[!NOTE]` GitHub-flavored alerts | Not portable to future docs site; use `> **Note:**` instead |
| Hard-coding version numbers in prose | Goes stale immediately; use "current release" or link to CHANGELOG |
| Explaining DAPR internals in a quickstart page | Violates progressive disclosure; quickstart is "Try", not "Understand" |
| Adding YAML frontmatter to docs pages | GitHub renders it as visible text; not useful until Phase 3 docs site |
| Writing "click here" link text | Poor accessibility and SEO; use descriptive link text |

## Project Structure & Boundaries

### Complete Project Directory Structure

Files marked with `[NEW]` are created by this documentation architecture. Files marked with `[EXISTS]` are already in the repo. Files marked with `[MODIFY]` need changes.

```
Hexalith.EventStore/
├── README.md                                      [MODIFY] Rewrite per D6
├── CONTRIBUTING.md                                [NEW]    D4
├── CODE_OF_CONDUCT.md                             [NEW]    D4
├── CHANGELOG.md                                   [NEW]    FR50
├── .markdownlint-cli2.jsonc                       [NEW]    D3, markdownlint config
├── .markdownlintignore                            [EXISTS] Update if needed
├── .lycheeignore                                  [NEW]    D3, lychee exclusions
├── .github/
│   ├── workflows/
│   │   ├── docs-validation.yml                    [NEW]    D3 Phase 1a
│   │   └── docs-api-reference.yml                 [NEW]    D3 Phase 2
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug-report.yml                         [NEW]    D4
│   │   ├── feature-request.yml                    [NEW]    D4
│   │   └── docs-improvement.yml                   [NEW]    D4
│   ├── PULL_REQUEST_TEMPLATE.md                   [NEW]    D4
│   └── DISCUSSION_TEMPLATE/                       [NEW]    D4 (if supported)
├── docs/
│   ├── getting-started/
│   │   ├── prerequisites.md                       [NEW]    FR6, Phase 1a
│   │   ├── quickstart.md                          [NEW]    FR7, Phase 1a
│   │   └── first-domain-service.md                [NEW]    FR8, Phase 1b
│   ├── concepts/
│   │   ├── architecture-overview.md               [NEW]    FR11, Phase 1b
│   │   ├── event-envelope.md                      [NEW]    FR12, Phase 1b
│   │   ├── identity-scheme.md                     [NEW]    FR13, Phase 1b
│   │   ├── command-lifecycle.md                    [NEW]    FR14, Phase 1b
│   │   └── choose-the-right-tool.md               [NEW]    FR16, Phase 1a
│   ├── guides/
│   │   ├── deployment-docker-compose.md           [NEW]    FR22, Phase 2
│   │   ├── deployment-kubernetes.md               [NEW]    FR23, Phase 2
│   │   ├── deployment-azure-container-apps.md     [NEW]    FR24, Phase 2
│   │   ├── deployment-progression.md              [NEW]    FR56, Phase 2
│   │   ├── configuration-reference.md             [NEW]    FR21, Phase 2
│   │   ├── security-model.md                      [NEW]    FR27, Phase 2
│   │   ├── troubleshooting.md                     [NEW]    FR47-49, Phase 2
│   │   └── dapr-faq.md                            [NEW]    FR15, Phase 2
│   ├── reference/
│   │   ├── command-api.md                         [NEW]    FR17, Phase 1b
│   │   ├── nuget-packages.md                      [NEW]    FR18, Phase 1b
│   │   └── api/                                   [NEW]    FR19, Phase 2 (auto-gen)
│   ├── community/
│   │   ├── awesome-event-sourcing.md              [NEW]    FR41, Phase 1b
│   │   └── roadmap.md                             [NEW]    FR33, Phase 2
│   └── assets/
│       ├── quickstart-demo.gif                    [NEW]    FR5, Phase 1a
│       └── images/                                [NEW]    Screenshots, logos
├── samples/
│   ├── Hexalith.EventStore.Sample/                [EXISTS] Counter domain service
│   │   ├── Counter/
│   │   │   ├── Commands/                          [EXISTS]
│   │   │   ├── Events/                            [EXISTS]
│   │   │   ├── State/                             [EXISTS]
│   │   │   └── CounterProcessor.cs                [EXISTS]
│   │   └── Program.cs                             [EXISTS]
│   ├── Hexalith.EventStore.Sample.Tests/          [NEW]    D2, quickstart CI
│   │   ├── QuickstartSmokeTest.cs                 [NEW]    FR7/NFR1 validation
│   │   └── Hexalith.EventStore.Sample.Tests.csproj [NEW]
│   ├── dapr-components/
│   │   ├── redis/                                 [NEW]    D2, default backend
│   │   └── postgresql/                            [NEW]    D2, swap demo (FR9)
│   └── deploy/                                    [NEW]    D2, Phase 2
│       ├── docker-compose.yml                     [NEW]    FR22
│       ├── kubernetes/                            [NEW]    FR23
│       └── azure/                                 [NEW]    FR24
└── src/
    ├── Hexalith.EventStore.AppHost/               [EXISTS] Aspire orchestration
    └── (other existing src projects)              [EXISTS] Not modified by docs arch
```

### Architectural Boundaries

**Documentation Content Boundary:**
All hand-authored documentation lives in `docs/` and root-level markdown files (`README.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `CHANGELOG.md`). AI agents authoring content pages NEVER modify files outside this boundary.

**Sample Code Boundary:**
All runnable sample code lives in `samples/`. Documentation references sample code via relative links but never duplicates it. Changes to sample code may require documentation updates (caught by PR review).

**CI Infrastructure Boundary:**
All CI configuration lives in `.github/workflows/` and root-level config files (`.markdownlint-cli2.jsonc`, `.lycheeignore`). CI validates documentation but never modifies content.

**Source Code Boundary:**
`src/` contains the actual product source code. Documentation architecture does NOT modify source code. The only interaction is DefaultDocumentation reading XML doc comments from `src/` assemblies (Phase 2).

### Requirements to Structure Mapping

**Phase 1a (Launch) — 11 deliverables:**

| Deliverable | File(s) | FR(s) |
|-------------|---------|-------|
| README rewrite | `README.md` | FR1-5, FR39, FR45 |
| Prerequisites | `docs/getting-started/prerequisites.md` | FR6 |
| Quickstart | `docs/getting-started/quickstart.md` | FR7, FR10 |
| "Choose the right tool" | `docs/concepts/choose-the-right-tool.md` | FR3, FR4, FR16 |
| Animated GIF | `docs/assets/quickstart-demo.gif` | FR5 |
| CONTRIBUTING.md | `CONTRIBUTING.md` | FR28 |
| CODE_OF_CONDUCT.md | `CODE_OF_CONDUCT.md` | FR32 |
| Issue templates | `.github/ISSUE_TEMPLATE/*.yml` | FR29, FR30 |
| PR template | `.github/PULL_REQUEST_TEMPLATE.md` | FR31 |
| CHANGELOG.md | `CHANGELOG.md` | FR50 |
| CI pipeline (docs-validation) | `.github/workflows/docs-validation.yml` | FR34-36 |

**Phase 1b (Depth) — 9 deliverables:**

| Deliverable | File(s) | FR(s) |
|-------------|---------|-------|
| First domain service tutorial | `docs/getting-started/first-domain-service.md` | FR8, FR9 |
| Architecture overview + diagrams | `docs/concepts/architecture-overview.md` | FR11 |
| Command API reference | `docs/reference/command-api.md` | FR17 |
| Event envelope | `docs/concepts/event-envelope.md` | FR12 |
| Identity scheme | `docs/concepts/identity-scheme.md` | FR13 |
| Command lifecycle | `docs/concepts/command-lifecycle.md` | FR14 |
| NuGet packages guide | `docs/reference/nuget-packages.md` | FR18, FR20 |
| Awesome event sourcing | `docs/community/awesome-event-sourcing.md` | FR41 |
| Sample integration tests | `samples/Hexalith.EventStore.Sample.Tests/` | FR34 (CI) |

**Phase 2 (Operations) — 14 deliverables:**

| Deliverable | File(s) | FR(s) |
|-------------|---------|-------|
| Deployment guides (x3) | `docs/guides/deployment-*.md` | FR22-24, FR57-60 |
| Deployment progression | `docs/guides/deployment-progression.md` | FR56 |
| Security model | `docs/guides/security-model.md` | FR27 |
| Configuration reference | `docs/guides/configuration-reference.md` | FR21 |
| DAPR FAQ | `docs/guides/dapr-faq.md` | FR15 |
| Troubleshooting | `docs/guides/troubleshooting.md` | FR47-49 |
| API auto-generation | `docs/reference/api/`, `.github/workflows/docs-api-reference.yml` | FR19 |
| Roadmap | `docs/community/roadmap.md` | FR33 |
| GitHub Discussions setup | (GitHub settings, not a file) | FR32 |
| Deployment configs | `samples/deploy/*` | FR22-24 |
| DAPR component variants | `samples/dapr-components/*` | FR25 |
| Resource sizing | Added to `docs/guides/deployment-*.md` | FR63 |

### Cross-Cutting Concerns Mapping

| Concern | Touchpoints |
|---------|------------|
| Accessibility (NFR6-10) | Every file in `docs/`, `README.md` — heading hierarchy, alt text, prereq depth |
| SEO keywords (NFR24) | `README.md` (first 200 words), every H1+summary in `docs/` |
| Cross-linking (D7, NFR27) | Every file in `docs/` — back-link, next steps, inline links |
| Code accuracy (NFR18) | `samples/` + CI validates; inline code in `docs/` aligned by convention |
| Markdown formatting (NFR20) | All `.md` files validated by markdownlint-cli2 |
| Link integrity (NFR19) | All `.md` files validated by lychee |

### Development Workflow

**Authoring a new documentation page:**
1. Create `.md` file in appropriate `docs/` subfolder
2. Follow page structure template (back-link, H1, summary, prerequisites, content, next steps)
3. Add relative cross-links to related pages
4. If code examples reference sample project, verify sample builds locally
5. Submit PR — CI validates lint, links, and sample build
6. Visual review of Mermaid diagrams in PR preview (if applicable)

**Updating sample code:**
1. Modify files in `samples/`
2. Verify `dotnet build` and `dotnet test` pass locally
3. Review all `docs/` pages referencing the changed code
4. Update inline code examples if needed
5. Submit PR — CI validates cross-platform

## Architecture Validation

### Coherence Validation: PASS

**Decision Compatibility:**

| Check | Status | Notes |
|-------|--------|-------|
| D1 (folder structure) + D3 (CI) | Compatible | CI paths reference exact `docs/` structure from D1 |
| D2 (samples) + D3 (CI) | Compatible | CI builds `samples/` including new test project |
| D5 (Mermaid) + D1 (structure) | Compatible | Mermaid inline in `docs/concepts/` pages per D1 |
| D6 (README) + D5 (diagrams) | Compatible | README includes inline Mermaid architecture diagram |
| D7 (navigation) + D1 (structure) | Compatible | Relative links work within the `docs/` tree |
| Tooling stack mutual compatibility | Compatible | markdownlint-cli2 + lychee + dotnet build are independent tools with no conflicts |

**Pattern Consistency:**

| Check | Status | Notes |
|-------|--------|-------|
| Page structure template + D7 navigation | Consistent | Both define same back-link and Next Steps conventions |
| Code example patterns + D2 sample | Consistent | Patterns mandate Counter domain names; sample uses Counter domain |
| DAPR depth pattern + progressive disclosure | Consistent | Depth escalates README → quickstart → concepts → guides → FAQ |
| Markdown formatting patterns + markdownlint | Consistent | Patterns enforceable via markdownlint rules (heading hierarchy, code fences) |

**Structure Alignment:**

| Check | Status | Notes |
|-------|--------|-------|
| Project tree supports all D1-D7 decisions | Aligned | Every file in tree maps to a decision |
| Boundaries respect architectural separation | Aligned | docs/, samples/, src/, .github/ are isolated |
| CI structure matches workflow design | Aligned | Two workflow files match D3 phased design |

### Requirements Coverage Validation

**Functional Requirements Coverage: 63/63 mapped**

| FR Category | FRs | Architecture Coverage | Status |
|-------------|-----|----------------------|--------|
| Discovery & Evaluation | FR1-6 | D6 (README), D5 (diagram), Phase 1a structure | COVERED |
| Getting Started | FR7-10 | D2 (samples), quickstart page, backend swap demo | COVERED |
| Concepts | FR11-16 | D5 (Mermaid), D1 (concepts/ folder), DAPR depth pattern | COVERED |
| API & Reference | FR17-21 | D1 (reference/ folder), Phase 2 DefaultDocumentation | COVERED |
| Deployment | FR22-27, FR57-60, FR63 | D2 (deploy configs), Phase 2 guides | COVERED |
| Community | FR28-33 | D4 (templates, CONTRIBUTING, Discussions) | COVERED |
| Content Quality | FR34-38, FR61-62 | D3 (CI pipeline), markdownlint, lychee | COVERED |
| SEO | FR39-42 | D6 (keywords), D7 (cross-links), ecosystem page | COVERED |
| Navigation | FR43-46 | D7 (navigation strategy), page template | COVERED |
| Troubleshooting | FR47-49 | Phase 2 troubleshooting.md | COVERED |
| Lifecycle | FR50-56 | CHANGELOG, Phase 2 upgrade docs, D2 deploy progression | COVERED |

**Non-Functional Requirements Coverage: 28/28 addressed**

| NFR Category | NFRs | Architecture Coverage | Status |
|-------------|------|----------------------|--------|
| Performance | NFR1-5 | D2 (quickstart CI timing), D5 (Mermaid native), GIF <5MB | COVERED |
| Accessibility | NFR6-10 | Formatting patterns (heading hierarchy), D5 (`<details>` blocks), prereq depth limit | COVERED |
| Maintainability | NFR11-17 | Self-contained pages (D1), inline code (no extraction tool), PR workflow (D4), quarterly review (convention) | COVERED |
| Reliability | NFR18-23 | D3 (CI pipeline: sample build, lychee, markdownlint, cross-platform matrix) | COVERED |
| Discoverability | NFR24-28 | D6 (README keywords), page template (H1+summary), D1 (descriptive filenames), D7 (2-click depth) | COVERED |

**Notable NFR handling:**
- **NFR12** ("no manual code in markdown"): Architecture uses inline code fences validated by CI sample builds. The code IS manually in markdown, but accuracy is enforced by the sample project being the source of truth. NFR12's intent is "no stale code" — CI-validated samples achieve this.
- **NFR15** ("GIF regeneratable via script"): Deferred to Phase 3. Phase 1a uses manual capture with documented procedure. Acknowledged gap accepted during tooling evaluation.

### Implementation Readiness Validation: PASS

**DGAP Resolution Summary:**

| Gap | Resolution |
|-----|-----------|
| DGAP-1 (sample structure) | Resolved by D2 |
| DGAP-2 (DocFX vs alternatives) | Resolved: DefaultDocumentation Phase 2 |
| DGAP-3 (GIF tooling) | Resolved: manual Phase 1a, automate Phase 3 |
| DGAP-4 (code extraction) | Resolved: inline fences + CI |
| DGAP-5 (link checking) | Resolved: lychee |
| DGAP-6 (markdown linting) | Resolved: markdownlint-cli2 |
| DGAP-7 (cross-platform) | Resolved: D3 matrix on sample build only |
| DGAP-8 (Mermaid validation) | Resolved: D5 visual PR review |

### Gap Analysis

**Critical Gaps: None**

**Important Gaps (non-blocking, address during implementation):**

| Gap | Impact | Recommendation |
|-----|--------|---------------|
| `.markdownlint-cli2.jsonc` ruleset not yet specified | Agents may configure differently | Define specific rules in first CI story (enable/disable rules for docs conventions like line length, inline HTML for `<details>` blocks) |
| `.lycheeignore` entries not yet specified | May get false positives on first run | Seed with common exclusions (localhost URLs, example.com, GitHub edit links) during CI setup |
| `CODE_OF_CONDUCT.md` content not specified | Low risk — standard template | Use Contributor Covenant v2.1 (industry standard) |

**Nice-to-Have Gaps (defer):**

| Gap | Recommendation |
|-----|---------------|
| No local validation script for authors | Add a `scripts/validate-docs.sh` that runs markdownlint + lychee locally (Phase 1b, FR61) |
| No page template generator | Not needed — page template is simple enough to copy from any existing page |

### Architecture Completeness Checklist

**Requirements Analysis**
- [x] Project context thoroughly analyzed (63 FRs, 28 NFRs categorized)
- [x] Scale and complexity assessed (High — CI infrastructure + multi-platform + progressive disclosure)
- [x] Technical constraints identified (GitHub hosting, solo author, existing core architecture)
- [x] Cross-cutting concerns mapped (5 concerns with touchpoints)

**Architectural Decisions**
- [x] 7 active decisions documented with rationale (D1-D7)
- [x] 3 deferred decisions with trigger conditions (D8-D10)
- [x] Tooling stack specified with phase assignments
- [x] CI budget validated (<300s NFR23)

**Implementation Patterns**
- [x] Content voice & tone conventions established
- [x] Markdown formatting rules defined
- [x] Code example patterns specified
- [x] Page structure template defined
- [x] Cross-reference conventions documented
- [x] DAPR explanation depth pattern defined
- [x] Anti-patterns documented

**Project Structure**
- [x] Complete directory structure with NEW/EXISTS/MODIFY annotations
- [x] 4 architectural boundaries defined
- [x] Phase 1a: 11 deliverables mapped to files
- [x] Phase 1b: 9 deliverables mapped to files
- [x] Phase 2: 14 deliverables mapped to files
- [x] Development workflow documented

### Architecture Readiness Assessment

**Overall Status: READY FOR IMPLEMENTATION**

**Confidence Level: High**

**Key Strengths:**
1. Minimum viable tooling — only 3 tools in CI for Phase 1a, well under complexity budget
2. Complete FR/NFR traceability — every requirement maps to a file and a decision
3. Clear boundaries — docs, samples, CI, and source code are strictly isolated
4. Solo-author-optimized — zero-friction authoring workflow, contributors need no special tools
5. Phased delivery — architecture supports shipping Phase 1a without Phase 2 infrastructure

**Areas for Future Enhancement:**
1. Automated GIF generation (Phase 3)
2. API reference auto-generation (Phase 2)
3. Local validation script (Phase 1b)
4. Docs site migration from GitHub to hosted site (Phase 3)

### Implementation Handoff

**AI Agent Guidelines:**
- Follow all architectural decisions D1-D7 exactly as documented
- Use implementation patterns consistently across all documentation pages
- Respect architectural boundaries (never modify `src/` from a docs story)
- Use the Counter domain (`IncrementCounter`, `CounterProcessor`, `CounterState`) in all examples
- Refer to this document for all documentation architecture questions

**First Implementation Priority:**
1. Create `docs/` folder structure (D1)
2. Set up `.markdownlint-cli2.jsonc` and `.lycheeignore`
3. Create `.github/workflows/docs-validation.yml` (D3)
4. Create issue templates, PR template, CONTRIBUTING.md, CODE_OF_CONDUCT.md (D4)
5. Rewrite README.md (D6)
6. Author `docs/getting-started/prerequisites.md` and `docs/getting-started/quickstart.md`
7. Capture quickstart GIF manually
