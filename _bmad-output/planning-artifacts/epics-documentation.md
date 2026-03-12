---
stepsCompleted:
  - step-01-validate-prerequisites
  - step-02-design-epics
  - step-03-create-stories
  - step-04-final-validation
inputDocuments:
  - prd-documentation.md
  - architecture-documentation.md
---

# Hexalith.EventStore Documentation - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Hexalith.EventStore Documentation & Developer Experience, decomposing the requirements from the PRD and Architecture into implementable stories.

## Requirements Inventory

### Functional Requirements

FR1: A .NET developer can understand what Hexalith.EventStore does within 30 seconds of landing on the README
FR2: A developer can see the core programming model (pure function contract) within the first screen scroll
FR3: A developer can self-assess whether Hexalith fits their needs through a structured decision aid
FR4: A developer can compare Hexalith's trade-offs against Marten, EventStoreDB, and custom implementations
FR5: A developer can see a visual demonstration of the system running before installing anything
FR6: A developer can identify all prerequisites needed before attempting the quickstart
FR7: A developer can clone the repository and have the sample application running with events flowing on a local Docker environment within 10 minutes
FR8: A developer can follow step-by-step instructions to build and register a custom domain service
FR9: A developer can experience an infrastructure backend swap (e.g., Redis to PostgreSQL) with zero code changes
FR10: A developer can send a test command and observe the resulting event in the event stream
FR11: A developer can learn the architecture topology without prior DAPR knowledge
FR12: A developer can understand the event envelope metadata structure
FR13: A developer can understand the identity scheme and how it maps to actors, streams, and topics
FR14: A developer can trace the end-to-end lifecycle of a command through the system
FR15: A developer can understand why DAPR was chosen and what trade-offs it introduces
FR16: A developer can understand when Hexalith is NOT the right choice for their project
FR17: A developer can look up any REST endpoint with request/response examples
FR18: A developer can determine which NuGet package to install for their use case
FR19: A developer can browse auto-generated API documentation for all public types
FR20: A developer can view the dependency relationships between NuGet packages
FR21: A developer can access a complete configuration reference for all system knobs
FR22: An operator can deploy the sample application to Docker Compose on a local development machine using a documented walkthrough
FR23: An operator can deploy the sample application to an on-premise Kubernetes cluster using a documented walkthrough
FR24: An operator can deploy the sample application to Azure Container Apps using a documented walkthrough
FR25: An operator can configure each DAPR component (State Store, Pub/Sub, Actors, Configuration, Resiliency) for their target infrastructure with documented examples per backend
FR26: An operator can verify system health through documented health/readiness endpoints
FR27: An operator can understand the security model and configure authentication
FR28: A developer can find and follow a contribution workflow with documented fork, branch, and PR steps
FR29: A developer can identify beginner-friendly contribution opportunities
FR30: A developer can file structured bug reports, feature requests, and documentation improvements
FR31: A developer can submit pull requests following a documented template and checklist
FR32: A developer can participate in community discussions organized by category
FR33: A developer can view the public product roadmap
FR34: A CI pipeline can validate that all code examples in documentation compile and run
FR35: A CI pipeline can detect broken links across all documentation pages
FR36: A CI pipeline can enforce markdown formatting standards
FR37: Documentation maintainers can identify stale content through automated checks
FR38: A documentation reviewer can verify changes through the same PR process as code
FR39: The README can be discovered through GitHub search for key terms (event sourcing, .NET, DAPR, multi-tenant)
FR40: Documentation pages can be indexed by search engines with descriptive URLs and structured headings
FR41: A developer browsing event sourcing resources can discover Hexalith through the curated ecosystem page
FR42: A developer can navigate between related documentation pages through cross-linking
FR43: A developer can enter the documentation at any page and orient themselves without reading prerequisite pages
FR44: A developer can navigate a progressive complexity path from simple concepts to advanced patterns
FR45: A developer can access architecture documentation directly from the README as a parallel entry point
FR46: A developer can identify their current position in the documentation structure
FR47: A developer can find troubleshooting guidance for quickstart errors including: Docker not running, port conflicts, DAPR sidecar timeout, .NET SDK version mismatch, and sample build failure
FR48: A developer can find documented solutions for DAPR integration issues including: sidecar injection failure, state store connection timeout, pub/sub message loss, actor activation conflict, and component configuration mismatch
FR49: A developer can access troubleshooting information for deployment failures per target environment
FR50: A developer can view a changelog of breaking changes and migration steps between releases
FR51: A developer can understand how event versioning and schema evolution are handled
FR52: A developer can follow a documented upgrade path when moving between major versions
FR53: A developer can set up a local development environment matching the documented configuration
FR54: A developer can identify the library version documented by a documentation page via a version reference in the README linking to the corresponding release tag
FR55: An operator can follow a documented disaster recovery procedure for the event store
FR56: A developer can follow a documented progression from the local Docker sample to on-premise Kubernetes to Azure cloud deployment using the same application code with only infrastructure configuration changes
FR57: A developer can understand and set up the DAPR runtime for each target environment (local Docker, Kubernetes, Azure) as a prerequisite to deploying the sample application
FR58: A developer can understand what infrastructure differences exist between local Docker, on-premise Kubernetes, and Azure cloud deployments and why each configuration differs
FR59: A developer who completed the local Docker quickstart can transition to a Kubernetes or Azure deployment guide with explicit references to what they already know and what's new
FR60: An operator can understand where event data is physically stored based on their DAPR state store configuration and what persistence guarantees each backend provides
FR61: A documentation contributor can run the full validation suite (code compilation, link checking, markdown linting) locally with a single command
FR62: A maintainer can verify that every functional requirement has at least one corresponding documentation page through a traceability check
FR63: An operator can determine resource requirements (CPU, memory, storage) and pod sizing guidance for production deployment per target environment (Docker Compose, Kubernetes, Azure Container Apps)

### NonFunctional Requirements

NFR1: The quickstart guide results in a running system within 10 minutes on a clean development machine with prerequisites installed (validated by CI timing)
NFR2: Any documentation page renders fully on GitHub (all markdown content and Mermaid diagrams visible) within 2 seconds on a 25 Mbps connection, validated by page file size staying under 200KB
NFR3: The animated README GIF is under 5MB in file size (validated by CI file size check)
NFR4: Mermaid diagrams produce visible, non-error diagrams when viewed on GitHub.com in Chrome, Firefox, and Edge latest versions, without requiring external tooling or browser extensions
NFR5: The "Build Your First Domain Service" tutorial completes within 1 hour for a developer familiar with .NET and DDD concepts, validated by a timed walkthrough during authoring and by tutorial CI job completion time
NFR6: All documentation pages use structured heading hierarchy (H1-H4) with no skipped levels, enabling screen reader navigation
NFR7: All architecture diagrams and Mermaid visuals include descriptive alt text that conveys the same information as the visual
NFR8: Color is never the sole indicator of meaning in any diagram, table, or callout — shape, label, or pattern must also distinguish elements
NFR9: Code examples include language-specific syntax highlighting tags for assistive technology compatibility
NFR10: No documentation page requires more than 2 prerequisite pages to understand (maximum depth of dependency)
NFR11: Documentation pages are self-contained markdown files with no cross-file build dependencies, enabling any single page to be updated and validated within one CI cycle
NFR12: No code examples in documentation are manually maintained in markdown files — all are extracted from or validated against runnable source projects
NFR13: Documentation changes follow the same PR review process as code changes, with no separate publishing workflow
NFR14: Stale content is detectable within 1 CI cycle after a breaking code change
NFR15: The animated README GIF can be regenerated via a single script or CI job, not manual screen recording
NFR16: Adding a new documentation page requires no changes to build configuration or CI pipeline — drop a markdown file in the correct folder
NFR17: Documentation structure supports a quarterly review cadence — each page has a clear owner (Jerome for MVP) and last-reviewed date is trackable via git history
NFR18: 100% of code examples in documentation compile and run successfully against the current release (validated by CI on every commit)
NFR19: Zero broken links across all documentation pages (validated by CI link checker on every commit)
NFR20: Markdown formatting passes linting rules consistently (validated by a markdown linting tool in CI)
NFR21: The quickstart produces identical results on macOS, Windows, and Linux development machines with Docker Desktop installed
NFR22: All three deployment walkthroughs (Docker Compose, Kubernetes, Azure) produce a verifiably running system when followed step-by-step
NFR23: CI validation pipeline completes in under 5 minutes to avoid blocking PR merges
NFR24: README contains all primary search keywords (event sourcing, .NET, DAPR, distributed, multi-tenant, event store, CQRS, DDD) within the first 200 words
NFR25: Every documentation page starts with a clear H1 title and a one-paragraph summary optimized for search engine snippets
NFR26: Documentation filenames use descriptive, unabbreviated words separated by hyphens (e.g., deployment-kubernetes.md not deploy-k8s.md)
NFR27: Cross-links between related documentation pages ensure no page is more than 2 clicks from the README
NFR28: NuGet package descriptions contain at least 3 keywords from the NFR24 keyword list and use terminology consistent with the documentation

### Additional Requirements

- Architecture specifies no starter template — this is a brownfield product with greenfield documentation
- D1: Content folder structure follows specific hierarchy (docs/getting-started/, docs/concepts/, docs/guides/, docs/reference/, docs/community/, docs/assets/)
- D2: Sample project architecture extends existing Counter domain in samples/Hexalith.EventStore.Sample/ — do not create new sample domains
- D2: New integration test project samples/Hexalith.EventStore.Sample.Tests/ needed for quickstart CI validation
- D2: DAPR component YAML variants needed in samples/dapr-components/ for Redis (default) and PostgreSQL (backend swap demo)
- D2: Deployment configurations needed in samples/deploy/ for Docker Compose, Kubernetes, and Azure
- D3: Two GitHub Actions workflows — docs-validation.yml (Phase 1a, PR+push triggers) and docs-api-reference.yml (Phase 2, release tag trigger)
- D3: CI pipeline uses markdownlint-cli2 for linting, lychee for link checking, dotnet build+test for sample validation
- D3: Cross-platform matrix (ubuntu, windows, macos) only for sample build/test; linting runs on ubuntu-latest only
- D3: CI budget must stay under 300s (NFR23) — Phase 1a estimated at ~125s
- D4: Community infrastructure includes GitHub Discussions (4 categories), issue templates (bug, feature, docs-improvement), PR template, CONTRIBUTING.md, CODE_OF_CONDUCT.md
- D5: Mermaid diagrams are hand-authored inline in markdown — no separate .mmd files, no automated browser testing
- D5: Every Mermaid diagram must include a `<details>` block with text description for accessibility (NFR7)
- D6: README follows specific progressive disclosure order: GIF demo, one-liner+badges, hook paragraph, pure function contract, comparison table, quickstart link, architecture diagram, doc links, contributing, license
- D7: Every docs page must include back-link to README, H1+summary, optional prerequisites (max 2), content, Next Steps footer
- D7: All internal links are relative paths; 2-click maximum depth from README (NFR27)
- Implementation patterns: second person voice, professional-casual tone, developer-to-developer perspective
- Code examples must use Counter domain names (IncrementCounter, CounterProcessor, CounterState) — never invent new domain names
- DAPR explanation depth follows progressive pattern: README (one sentence) > Quickstart (functional) > Concepts (architectural) > Guides (operational) > FAQ (deep)
- Phase 1a deliverables: README rewrite, prerequisites, quickstart, choose-the-right-tool, GIF, CONTRIBUTING.md, CODE_OF_CONDUCT.md, issue templates, PR template, CHANGELOG.md, docs-validation CI
- Phase 1b deliverables: first-domain-service tutorial, architecture-overview, command-api, event-envelope, identity-scheme, command-lifecycle, nuget-packages, awesome-event-sourcing, sample integration tests
- Phase 2 deliverables: 3 deployment guides, deployment-progression, security-model, configuration-reference, dapr-faq, troubleshooting, API auto-generation, roadmap, GitHub Discussions setup, deployment configs, DAPR component variants, resource sizing
- .markdownlint-cli2.jsonc ruleset needs to be defined during first CI story
- .lycheeignore needs seeding with common exclusions (localhost, example.com, GitHub edit links)
- CODE_OF_CONDUCT.md uses Contributor Covenant v2.1
- Local validation script (scripts/validate-docs.sh) needed for FR61 in Phase 1b

### FR Coverage Map

| FR | Epic | Brief Description |
|----|------|-------------------|
| FR1 | Epic 1 | 30-second README understanding |
| FR2 | Epic 1 | Pure function contract visible |
| FR3 | Epic 1 | Structured decision aid (choose-the-right-tool) |
| FR4 | Epic 1 | Comparison vs Marten/EventStoreDB/custom |
| FR5 | Epic 1 | Animated GIF demo |
| FR6 | Epic 1 | Prerequisites identification |
| FR7 | Epic 2 | 10-minute quickstart |
| FR8 | Epic 5 | First domain service tutorial |
| FR9 | Epic 5 | Backend swap demo (Redis to PostgreSQL) |
| FR10 | Epic 2 | Send command, observe event |
| FR11 | Epic 5 | Architecture topology without DAPR knowledge |
| FR12 | Epic 5 | Event envelope metadata |
| FR13 | Epic 5 | Identity scheme |
| FR14 | Epic 5 | Command lifecycle |
| FR15 | Epic 5 | DAPR trade-offs |
| FR16 | Epic 5 | When NOT to use Hexalith |
| FR17 | Epic 5 | REST endpoint reference |
| FR18 | Epic 5 | NuGet package guide |
| FR19 | Epic 8 | Auto-generated API docs |
| FR20 | Epic 5 | NuGet package dependencies |
| FR21 | Epic 8 | Configuration reference |
| FR22 | Epic 7 | Docker Compose deployment |
| FR23 | Epic 7 | Kubernetes deployment |
| FR24 | Epic 7 | Azure Container Apps deployment |
| FR25 | Epic 7 | DAPR component configuration per backend |
| FR26 | Epic 7 | Health/readiness endpoints |
| FR27 | Epic 7 | Security model |
| FR28 | Epic 3 | Contribution workflow |
| FR29 | Epic 3 | Beginner-friendly issues |
| FR30 | Epic 3 | Structured issue templates |
| FR31 | Epic 3 | PR template & checklist |
| FR32 | Epic 3 | GitHub Discussions |
| FR33 | Epic 8 | Public roadmap |
| FR34 | Epic 4 | CI code example validation |
| FR35 | Epic 4 | CI broken link detection |
| FR36 | Epic 4 | CI markdown linting |
| FR37 | Epic 4 | Stale content detection |
| FR38 | Epic 3 | PR review process for docs |
| FR39 | Epic 1 | GitHub search discoverability |
| FR40 | Epic 1 | Search engine indexing |
| FR41 | Epic 5 | Awesome event sourcing ecosystem page |
| FR42 | Epic 2 | Cross-linking between pages |
| FR43 | Epic 1 | Self-contained page entry |
| FR44 | Epic 1 | Progressive complexity path |
| FR45 | Epic 1 | Architecture as parallel README entry |
| FR46 | Epic 1 | Position identification in docs |
| FR47 | Epic 7 | Quickstart troubleshooting |
| FR48 | Epic 7 | DAPR integration troubleshooting |
| FR49 | Epic 7 | Deployment failure troubleshooting |
| FR50 | Epic 1 | CHANGELOG |
| FR51 | Epic 8 | Event versioning & schema evolution |
| FR52 | Epic 8 | Upgrade path documentation |
| FR53 | Epic 1 | Local dev environment setup |
| FR54 | Epic 1 | Version reference in README |
| FR55 | Epic 7 | Disaster recovery procedure |
| FR56 | Epic 7 | Deployment progression path |
| FR57 | Epic 7 | DAPR runtime setup per environment |
| FR58 | Epic 7 | Infrastructure differences documentation |
| FR59 | Epic 7 | Quickstart-to-deployment transition |
| FR60 | Epic 7 | Event data storage per backend |
| FR61 | Epic 6 | Local validation suite |
| FR62 | Epic 6 | FR traceability check |
| FR63 | Epic 7 | Resource sizing guidance |

## Epic List

### Epic 1: Foundation & First Impression (Phase 1a)
Developers landing on the repo can immediately understand what Hexalith.EventStore does, decide if it fits their needs, and find the quickstart. The repo has a professional, complete README with CI quality gates protecting all content.
**FRs covered:** FR1, FR2, FR3, FR4, FR5, FR6, FR39, FR40, FR43, FR44, FR45, FR46, FR50, FR53, FR54

### Epic 2: Quickstart & Onboarding (Phase 1a)
Developers can clone the repo and have the sample running within 10 minutes, send a command, and see events flowing. The getting-started path is complete end-to-end.
**FRs covered:** FR7, FR10, FR42

### Epic 3: Community Infrastructure (Phase 1a)
Contributors can find contribution guidelines, file structured issues, submit PRs, and participate in discussions. The open-source community scaffolding is complete.
**FRs covered:** FR28, FR29, FR30, FR31, FR32, FR38

### Epic 4: Documentation CI Pipeline (Phase 1a)
Every documentation PR is automatically validated for markdown formatting, broken links, and sample code compilation across platforms. Quality gates prevent documentation debt.
**FRs covered:** FR34, FR35, FR36, FR37

### Epic 5: Concept Deep Dives & Technical Reference (Phase 1b)
Developers who tried the quickstart can now understand the architecture, trace command lifecycles, learn the identity scheme, and look up API endpoints and NuGet packages.
**FRs covered:** FR8, FR9, FR11, FR12, FR13, FR14, FR15, FR16, FR17, FR18, FR20, FR41

### Epic 6: Sample Integration Tests & Local Validation (Phase 1b)
The quickstart is validated by CI through integration tests, and documentation contributors can run the full validation suite locally with a single command.
**FRs covered:** FR61, FR62

### Epic 7: Deployment & Operations Guides (Phase 2)
Operators can deploy the sample to Docker Compose, Kubernetes, and Azure Container Apps with documented walkthroughs, configure DAPR components per backend, and understand the security model.
**FRs covered:** FR22, FR23, FR24, FR25, FR26, FR27, FR47, FR48, FR49, FR55, FR56, FR57, FR58, FR59, FR60, FR63

### Epic 8: Configuration, Versioning & Lifecycle (Phase 2)
Developers can access a complete configuration reference, understand event versioning, follow upgrade paths between versions, and view the product roadmap.
**FRs covered:** FR19, FR21, FR33, FR51, FR52

## Epic 1: Foundation & First Impression

Developers landing on the repo can immediately understand what Hexalith.EventStore does, decide if it fits their needs, and find the quickstart. The repo has a professional, complete README with CI quality gates protecting all content.

### Story 1.1: Documentation Folder Structure & Page Conventions

As a documentation contributor,
I want a well-organized docs folder structure with naming conventions in place,
So that I know exactly where to create new pages and all future content has a consistent home.

**Acceptance Criteria:**

**Given** the repository has no `docs/` folder
**When** this story is complete
**Then** the following folder structure exists: `docs/getting-started/`, `docs/concepts/`, `docs/guides/`, `docs/reference/`, `docs/community/`, `docs/assets/`, `docs/assets/images/`
**And** all folder names use lowercase, hyphen-separated, descriptive names per NFR26
**And** a placeholder `.gitkeep` file exists in each empty folder so Git tracks the structure

### Story 1.2: README Rewrite with Progressive Disclosure

As a .NET developer evaluating event sourcing solutions,
I want to land on the README and immediately understand what Hexalith.EventStore does, see the programming model, and compare it to alternatives,
So that I can decide within 30 seconds whether to invest more time.

**Acceptance Criteria:**

**Given** a developer navigates to the repository root
**When** they view the README
**Then** the first viewport contains: placeholder for animated GIF demo, one-liner description with badge row (stars, NuGet, build status, license), hook paragraph ("If you've spent weeks wiring up an event store..."), pure function contract code block (`(Command, CurrentState?) -> List<DomainEvent>`), comparison table vs Marten/EventStoreDB/custom, and prominent quickstart link
**And** below the fold: inline Mermaid architecture diagram with `<details>` text description for accessibility (NFR7), documentation links organized by funnel stage, contributing link, and MIT license
**And** the first 200 words contain all primary SEO keywords: event sourcing, .NET, DAPR, distributed, multi-tenant, event store, CQRS, DDD (NFR24)
**And** the README uses structured heading hierarchy H1-H4 with no skipped levels (NFR6)
**And** all code blocks specify language tags (NFR9)

### Story 1.3: Prerequisites & Local Dev Environment Page

As a developer ready to try Hexalith,
I want a clear prerequisites page listing everything I need installed,
So that I can set up my local environment before starting the quickstart.

**Acceptance Criteria:**

**Given** a developer navigates to `docs/getting-started/prerequisites.md`
**When** they read the page
**Then** the page follows the standard page template: back-link to README, H1 title, one-paragraph summary, content, Next Steps footer
**And** the page lists all prerequisites: .NET 10 SDK, Docker Desktop, DAPR CLI with version numbers
**And** each prerequisite includes a verification command (e.g., `$ dotnet --version`)
**And** the page links to the quickstart as the Next Step
**And** the page is self-contained (FR43) — a developer arriving from search understands it without reading other pages
**And** all internal links use relative paths

### Story 1.4: Choose the Right Tool Decision Aid

As a developer evaluating event sourcing solutions,
I want a structured decision aid that helps me assess whether Hexalith fits my project,
So that I can make an informed technology choice before investing time.

**Acceptance Criteria:**

**Given** a developer navigates to `docs/concepts/choose-the-right-tool.md`
**When** they read the page
**Then** the page includes a detailed comparison of Hexalith vs Marten, EventStoreDB, and custom implementations (FR4)
**And** the page includes a structured self-assessment (e.g., decision matrix or checklist) for whether Hexalith fits their needs (FR3)
**And** the page honestly describes when Hexalith is NOT the right choice (FR16) — including scenarios like non-.NET stacks, sub-millisecond latency requirements, or no container orchestration
**And** the page follows the standard page template with back-link, H1, summary, content, Next Steps
**And** the page is self-contained (FR43)

### Story 1.5: Animated GIF Demo Capture

As a developer evaluating Hexalith,
I want to see the system running visually before installing anything,
So that I get a concrete sense of what the experience looks like.

**Acceptance Criteria:**

**Given** the quickstart sample application is running
**When** the GIF is captured
**Then** `docs/assets/quickstart-demo.gif` shows the Aspire dashboard with services running, a command being sent, and an event appearing in the stream
**And** the GIF file size is under 5MB (NFR3)
**And** the README references the GIF via relative path `docs/assets/quickstart-demo.gif`
**And** a documented regeneration procedure (checklist of steps to reproduce) exists in the repo

### Story 1.6: CHANGELOG Initialization

As a developer tracking Hexalith releases,
I want a CHANGELOG documenting breaking changes and migration steps,
So that I can understand what changed between versions and how to upgrade.

**Acceptance Criteria:**

**Given** the repository has no CHANGELOG.md
**When** this story is complete
**Then** `CHANGELOG.md` exists at the repository root
**And** it follows the "Keep a Changelog" format with sections for Added, Changed, Deprecated, Removed, Fixed, Security
**And** it contains at minimum the current release entry
**And** the README links to CHANGELOG.md
**And** the version reference in the README links to the corresponding release tag (FR54)

## Epic 2: Quickstart & Onboarding

Developers can clone the repo and have the sample running within 10 minutes, send a command, and see events flowing. The getting-started path is complete end-to-end.

### Story 2.1: Quickstart Guide

As a .NET developer,
I want step-by-step instructions to clone the repo and run the sample application with events flowing locally,
So that I can experience the system working within 10 minutes.

**Acceptance Criteria:**

**Given** a developer has completed all prerequisites (Docker Desktop, .NET 10 SDK, DAPR CLI)
**When** they follow `docs/getting-started/quickstart.md`
**Then** the guide walks them through: clone the repo, run the sample via `dotnet aspire run` (or equivalent), send a test command to the Counter domain, and observe the resulting event in the event stream
**And** the guide completes in under 10 minutes on a clean machine with prerequisites installed (NFR1)
**And** the guide works identically on macOS, Windows, and Linux with Docker Desktop (NFR21)
**And** DAPR is explained at functional depth ("DAPR handles message delivery and state storage — you don't write infrastructure code") per the progressive explanation pattern
**And** the page follows the standard page template: back-link to README, H1, summary, prerequisites link to `prerequisites.md`, content, Next Steps footer
**And** inline code examples use the Counter domain names (`IncrementCounter`, `CounterProcessor`, `CounterState`)
**And** cross-links to related pages (prerequisites, architecture overview, choose-the-right-tool) use relative paths (FR42)
**And** the page is self-contained (FR43) — a developer arriving from search can orient themselves

## Epic 3: Community Infrastructure

Contributors can find contribution guidelines, file structured issues, submit PRs, and participate in discussions. The open-source community scaffolding is complete.

### Story 3.1: CONTRIBUTING.md & CODE_OF_CONDUCT.md

As a developer who wants to contribute to Hexalith,
I want clear contribution guidelines and a code of conduct,
So that I know the expected workflow (fork, branch, PR) and community standards.

**Acceptance Criteria:**

**Given** the repository has no contribution documentation
**When** this story is complete
**Then** `CONTRIBUTING.md` exists at the repository root with sections: How to contribute (fork, branch, PR), Development setup (prerequisites, clone, build), Documentation contributions (edit markdown, run lint locally), Code contributions (coding standards, test requirements), Good first issues label explained, Community guidelines (link to CODE_OF_CONDUCT.md)
**And** `CODE_OF_CONDUCT.md` exists at the repository root using Contributor Covenant v2.1
**And** the README links to CONTRIBUTING.md
**And** both files follow markdown formatting standards (NFR6, NFR9)

### Story 3.2: GitHub Issue Templates

As a developer who found a bug or wants to request a feature,
I want structured issue templates that guide me through filing a useful report,
So that maintainers have the information they need to act on my feedback.

**Acceptance Criteria:**

**Given** the repository has no issue templates
**When** this story is complete
**Then** `.github/ISSUE_TEMPLATE/bug-report.yml` exists with fields: steps to reproduce, expected/actual behavior, environment (OS, .NET version, DAPR version)
**And** `.github/ISSUE_TEMPLATE/feature-request.yml` exists with fields: problem description, proposed solution, alternatives considered
**And** `.github/ISSUE_TEMPLATE/docs-improvement.yml` exists with fields: page/section, what's wrong or missing, suggested fix
**And** issues created from each template include the correct labels (e.g., `bug`, `enhancement`, `documentation`)
**And** the `good first issue` label is defined for beginner-friendly contributions (FR29)

### Story 3.3: PR Template & Review Process

As a contributor submitting a pull request,
I want a PR template with a checklist so I know what's expected,
So that my PR meets quality standards and gets reviewed efficiently.

**Acceptance Criteria:**

**Given** the repository has no PR template
**When** this story is complete
**Then** `.github/PULL_REQUEST_TEMPLATE.md` exists with checklist items: description of changes, related issue (if any), markdown lint passes locally, links not broken, `dotnet build` passes, `dotnet test` passes
**And** documentation changes follow the same PR review process as code changes (NFR13, FR38)

### Story 3.4: GitHub Discussions Setup

As a developer with questions about Hexalith,
I want organized community discussion categories,
So that I can ask questions, propose ideas, and share my work in the right place.

**Acceptance Criteria:**

**Given** the repository has no GitHub Discussions enabled
**When** this story is complete
**Then** GitHub Discussions is enabled with 4 categories: Announcements (release announcements, breaking changes), Q&A (technical questions with mark-answer enabled), Ideas (feature proposals, RFCs), Show & Tell (community projects, integrations, blog posts)
**And** the CONTRIBUTING.md references Discussions as a community channel
**And** the README links to Discussions

## Epic 4: Documentation CI Pipeline

Every documentation PR is automatically validated for markdown formatting, broken links, and sample code compilation across platforms. Quality gates prevent documentation debt.

### Story 4.1: Markdown Linting Configuration

As a documentation contributor,
I want automated markdown formatting validation on every PR,
So that all documentation pages maintain consistent formatting standards.

**Acceptance Criteria:**

**Given** the repository has no markdown linting configuration
**When** this story is complete
**Then** `.markdownlint-cli2.jsonc` exists at the repository root with rules configured for: heading hierarchy enforcement (no skipped levels, NFR6), code block language tag requirement (NFR9), no hard line wrapping, allowance for inline HTML (`<details>` blocks for Mermaid accessibility)
**And** the existing `.markdownlintignore` is updated to exclude non-documentation files as needed
**And** `markdownlint-cli2` can be run locally with `npx markdownlint-cli2 "docs/**/*.md" "README.md" "CONTRIBUTING.md" "CHANGELOG.md"`
**And** the linting rules align with the markdown formatting patterns in the architecture document (D3)

### Story 4.2: Link Checking Configuration

As a documentation maintainer,
I want automated broken link detection on every PR,
So that zero broken links exist across all documentation pages.

**Acceptance Criteria:**

**Given** the repository has no link checking configuration
**When** this story is complete
**Then** `.lycheeignore` exists at the repository root with common exclusions: localhost URLs, example.com, GitHub edit links, and any known false positives
**And** `lychee` can be run locally to check all markdown files
**And** `.lycheecache` is added to `.gitignore` for caching across local runs
**And** the configuration supports both relative links (internal docs) and external URLs

### Story 4.3: Documentation Validation GitHub Actions Workflow

As a documentation maintainer,
I want a CI pipeline that validates markdown linting, link integrity, and sample code compilation on every PR and push to main,
So that quality gates prevent documentation debt automatically.

**Acceptance Criteria:**

**Given** the markdown linting and link checking configurations exist (Stories 4.1, 4.2)
**When** this story is complete
**Then** `.github/workflows/docs-validation.yml` exists and triggers on PR and push to main
**And** the workflow has two jobs: `lint-and-links` (ubuntu-latest, ~35s) running markdownlint-cli2 and lychee on docs, README, CONTRIBUTING, CHANGELOG; and `sample-build` (matrix: ubuntu-latest, windows-latest, macos-latest, ~90s) running `dotnet build` and `dotnet test` on `samples/`
**And** both jobs are blocking — any failure prevents merge
**And** the workflow uses caching: lychee cache (`.lycheecache`), NuGet package cache, dotnet restore cache
**And** total CI pipeline completes in under 5 minutes (NFR23) — target ~125s for Phase 1a
**And** the README build status badge references this workflow

### Story 4.4: Stale Content Detection

As a documentation maintainer,
I want to identify stale documentation content within one CI cycle after a breaking code change,
So that I can update affected pages before users encounter outdated information.

**Acceptance Criteria:**

**Given** the CI pipeline validates sample code compilation (Story 4.3)
**When** a code change in `samples/` breaks the build
**Then** the CI pipeline fails, signaling that documentation-referenced code has changed (NFR14)
**And** the sample build failure identifies which project or test failed, making it clear which documentation pages may need updates
**And** stale content is detectable within one CI cycle (FR37)

## Epic 5: Concept Deep Dives & Technical Reference

Developers who tried the quickstart can now understand the architecture, trace command lifecycles, learn the identity scheme, and look up API endpoints and NuGet packages.

### Story 5.1: Architecture Overview with Mermaid Topology

As a developer who completed the quickstart,
I want to understand the system architecture without needing prior DAPR knowledge,
So that I can reason about how components interact before building my own services.

**Acceptance Criteria:**

**Given** a developer navigates to `docs/concepts/architecture-overview.md`
**When** they read the page
**Then** the page explains the architecture topology (services, DAPR sidecars, state stores, pub/sub) using an inline Mermaid diagram (C4 Context or flowchart)
**And** every Mermaid diagram has a `<details>` block with text description for accessibility (NFR7)
**And** color is never the sole indicator of meaning — shape, label, or pattern also distinguish elements (NFR8)
**And** DAPR is explained at architectural depth: which building blocks are used and why, with links to DAPR docs for depth
**And** the page follows the standard page template with back-link, H1, summary, max 2 prerequisites, content, Next Steps
**And** the page is self-contained (FR43)
**And** the Mermaid diagram renders natively on GitHub without external tooling (NFR4)

### Story 5.2: Command Lifecycle Deep Dive

As a developer building on Hexalith,
I want to trace the end-to-end lifecycle of a command through the system,
So that I understand the complete flow from API call to persisted event.

**Acceptance Criteria:**

**Given** a developer navigates to `docs/concepts/command-lifecycle.md`
**When** they read the page
**Then** the page traces a command from REST API receipt through routing, actor activation, domain processing, event emission, and state persistence using a Mermaid sequence diagram
**And** every Mermaid diagram has a `<details>` text description (NFR7)
**And** code examples use Counter domain names (`IncrementCounter`, `CounterProcessor`, `CounterState`)
**And** the page follows the standard page template
**And** the page is self-contained with max 2 prerequisites (NFR10)

### Story 5.3: Event Envelope Metadata Structure

As a developer working with Hexalith events,
I want to understand the event envelope metadata structure,
So that I know what metadata accompanies every event and how to use it.

**Acceptance Criteria:**

**Given** a developer navigates to `docs/concepts/event-envelope.md`
**When** they read the page
**Then** the page explains the complete metadata structure of an event envelope with field descriptions
**And** includes a JSON or C# example of a real event envelope from the Counter domain
**And** the page follows the standard page template
**And** the page is self-contained (FR43)

### Story 5.4: Identity Scheme Documentation

As a developer configuring aggregates and streams,
I want to understand the identity scheme and how it maps to actors, streams, and topics,
So that I can correctly structure my domain identifiers.

**Acceptance Criteria:**

**Given** a developer navigates to `docs/concepts/identity-scheme.md`
**When** they read the page
**Then** the page explains the `tenant:domain:aggregate-id` identity pattern and how it maps to DAPR actors, event streams, and pub/sub topics
**And** includes an inline Mermaid flowchart showing the mapping with `<details>` text description (NFR7)
**And** uses Counter domain examples for concrete identity values
**And** the page follows the standard page template
**And** the page is self-contained (FR43)

### Story 5.5: DAPR Trade-offs & FAQ Intro

As a developer evaluating Hexalith's technology choices,
I want to understand why DAPR was chosen and what trade-offs it introduces,
So that I can assess the risk and benefits for my project.

**Acceptance Criteria:**

**Given** a developer navigates to `docs/concepts/choose-the-right-tool.md` (DAPR section) or follows a link from the architecture overview
**When** they read the DAPR-specific content
**Then** the content explains: why DAPR was chosen (infrastructure portability, building blocks), what trade-offs it introduces (runtime dependency, learning curve, version coupling), and what happens if DAPR changes direction
**And** this content is integrated into the existing choose-the-right-tool page (FR15, FR16) or linked from it
**And** DAPR is explained at architectural depth per the progressive explanation pattern

### Story 5.6: First Domain Service Tutorial

As a developer who completed the quickstart,
I want step-by-step instructions to build and register my own domain service,
So that I can extend the system with my business logic.

**Acceptance Criteria:**

**Given** a developer has completed the quickstart (Story 2.1)
**When** they follow `docs/getting-started/first-domain-service.md`
**Then** the tutorial walks them through creating a new domain service: defining commands, events, state, and a processor — following the same pattern as the Counter domain
**And** the tutorial includes a backend swap demonstration (FR9): switching from Redis to PostgreSQL with zero code changes by changing DAPR component YAML
**And** the tutorial completes within 1 hour for a .NET/DDD-familiar developer (NFR5)
**And** all code examples use inline code fences with language tags and are aligned with the sample project
**And** the page follows the standard page template with prerequisites linking to quickstart
**And** the tutorial references the DAPR component YAML variants in `samples/dapr-components/` (D2)

### Story 5.7: Command API Reference

As a developer integrating with Hexalith's REST API,
I want to look up any REST endpoint with request/response examples,
So that I can build clients or test integrations.

**Acceptance Criteria:**

**Given** a developer navigates to `docs/reference/command-api.md`
**When** they read the page
**Then** the page documents all REST endpoints with: HTTP method, URL path, request body schema, response body schema, example `curl` commands, and expected responses
**And** examples use Counter domain commands (`IncrementCounter`, `DecrementCounter`, `ResetCounter`)
**And** the page follows the standard page template
**And** the page is self-contained (FR43)

### Story 5.8: NuGet Packages Guide & Dependency Graph

As a developer adding Hexalith packages to their project,
I want to know which NuGet package to install for my use case and see the dependency relationships,
So that I install only what I need.

**Acceptance Criteria:**

**Given** a developer navigates to `docs/reference/nuget-packages.md`
**When** they read the page
**Then** the page lists all public NuGet packages with: package name, description, primary use case, and when to install it
**And** the page includes an inline Mermaid flowchart showing package dependency relationships (FR20) with `<details>` text description (NFR7)
**And** package descriptions use terminology consistent with the documentation (NFR28)
**And** the page follows the standard page template

### Story 5.9: Awesome Event Sourcing Ecosystem Page

As a developer exploring event sourcing resources,
I want a curated ecosystem page linking to related tools, libraries, and learning resources,
So that I can discover the broader ecosystem and evaluate Hexalith in context.

**Acceptance Criteria:**

**Given** a developer navigates to `docs/community/awesome-event-sourcing.md`
**When** they read the page
**Then** the page lists curated resources organized by category: event sourcing frameworks (.NET and other), learning resources (articles, talks, books), related DAPR projects, and complementary tools
**And** the page includes Hexalith's positioning among the listed resources
**And** the page follows the standard page template
**And** all external links use descriptive text (not "click here")

## Epic 6: Sample Integration Tests & Local Validation

The quickstart is validated by CI through integration tests, and documentation contributors can run the full validation suite locally with a single command.

### Story 6.1: Sample Integration Test Project

As a documentation maintainer,
I want an integration test project that validates the quickstart scenario in CI,
So that I know the documented quickstart produces a working system on every commit.

**Acceptance Criteria:**

**Given** the sample project exists at `samples/Hexalith.EventStore.Sample/`
**When** this story is complete
**Then** `samples/Hexalith.EventStore.Sample.Tests/` exists with a project file (`Hexalith.EventStore.Sample.Tests.csproj`)
**And** `QuickstartSmokeTest.cs` exists and validates the core quickstart scenario: send an `IncrementCounter` command, assert the resulting event appears in the event stream
**And** `dotnet test samples/Hexalith.EventStore.Sample.Tests/` passes on all three platforms (ubuntu, windows, macos)
**And** the test project is automatically picked up by the existing CI `sample-build` job (Story 4.3) with no CI configuration changes needed (NFR16)
**And** 100% of documented code examples are validated by this test or by sample build success (NFR18)

### Story 6.2: DAPR Component Variants for Backend Swap Demo

As a developer following the first domain service tutorial,
I want pre-configured DAPR component YAML files for Redis and PostgreSQL,
So that I can experience a backend swap with zero code changes.

**Acceptance Criteria:**

**Given** the sample project uses DAPR for infrastructure
**When** this story is complete
**Then** `samples/dapr-components/redis/` contains DAPR component YAML files configured for Redis (default local dev backend)
**And** `samples/dapr-components/postgresql/` contains DAPR component YAML files configured for PostgreSQL
**And** the backend swap is achievable by pointing DAPR to a different component directory — no application code changes
**And** each YAML file includes inline comments explaining every field per the code example patterns in the architecture document

### Story 6.3: Local Validation Script

As a documentation contributor,
I want to run the full validation suite locally with a single command,
So that I can verify my changes pass CI before submitting a PR.

**Acceptance Criteria:**

**Given** the CI pipeline validates markdown linting, link checking, and sample compilation
**When** this story is complete
**Then** `scripts/validate-docs.sh` (and `scripts/validate-docs.ps1` for Windows) exists and runs: markdownlint-cli2 on all documentation files, lychee link checking, and `dotnet build` + `dotnet test` on the samples
**And** the script exits with a non-zero code if any validation fails
**And** the script output clearly indicates which validation step failed
**And** CONTRIBUTING.md references the validation script in the "Documentation contributions" section

### Story 6.4: FR Traceability Check

As a documentation maintainer,
I want to verify that every functional requirement has at least one corresponding documentation page,
So that I can identify coverage gaps before they reach users.

**Acceptance Criteria:**

**Given** the epics document maps all 63 FRs to documentation pages
**When** this story is complete
**Then** a traceability mapping document or script exists that lists each FR number alongside the documentation page(s) that address it
**And** any FR without a corresponding documentation page is flagged as a gap
**And** the check can be run manually (a markdown table or script output) — automated CI enforcement is not required for MVP

## Epic 7: Deployment & Operations Guides

Operators can deploy the sample to Docker Compose, Kubernetes, and Azure Container Apps with documented walkthroughs, configure DAPR components per backend, and understand the security model.

### Story 7.1: Docker Compose Deployment Guide & Configuration

As an operator deploying Hexalith locally,
I want a step-by-step walkthrough for deploying the sample application to Docker Compose,
So that I can run the system on my development machine with a production-like topology.

**Acceptance Criteria:**

**Given** the sample project exists and the quickstart is documented
**When** this story is complete
**Then** `docs/guides/deployment-docker-compose.md` exists with a complete walkthrough: prerequisites, DAPR runtime setup for local Docker (FR57), step-by-step deployment instructions, verification of system health via health/readiness endpoints (FR26)
**And** `samples/deploy/docker-compose.yml` exists with a working Docker Compose configuration
**And** the guide includes an inline Mermaid deployment topology diagram with `<details>` text description (NFR7)
**And** the guide explains where event data is physically stored based on the DAPR state store configuration (FR60)
**And** the guide includes resource requirements (CPU, memory, storage) for local deployment (FR63)
**And** the walkthrough produces a verifiably running system when followed step-by-step (NFR22)
**And** the page follows the standard page template with DAPR explained at operational depth

### Story 7.2: Kubernetes Deployment Guide & Configuration

As an operator deploying Hexalith to an on-premise cluster,
I want a step-by-step walkthrough for deploying the sample application to Kubernetes,
So that I can run the system in a production environment.

**Acceptance Criteria:**

**Given** a developer has completed the local Docker quickstart
**When** they follow `docs/guides/deployment-kubernetes.md`
**Then** the guide includes: DAPR runtime setup for Kubernetes (FR57), step-by-step deployment instructions, Kubernetes YAML manifests, DAPR component configuration for Kubernetes, and health/readiness verification (FR26)
**And** `samples/deploy/kubernetes/` contains all necessary Kubernetes manifests and DAPR component configs
**And** the guide explicitly references what the reader already knows from the Docker quickstart and what's new (FR59)
**And** the guide explains infrastructure differences between local Docker and Kubernetes (FR58)
**And** the guide includes resource requirements and pod sizing guidance (FR63)
**And** event data storage location is documented per backend (FR60)
**And** the walkthrough produces a verifiably running system (NFR22)
**And** the page follows the standard page template

### Story 7.3: Azure Container Apps Deployment Guide & Configuration

As an operator deploying Hexalith to Azure,
I want a step-by-step walkthrough for deploying the sample application to Azure Container Apps,
So that I can run the system in a cloud-managed environment.

**Acceptance Criteria:**

**Given** a developer has completed the local Docker quickstart
**When** they follow `docs/guides/deployment-azure-container-apps.md`
**Then** the guide includes: DAPR runtime setup for Azure (FR57), step-by-step deployment instructions, Azure resource provisioning (Container Apps Environment, managed DAPR), DAPR component configuration for Azure services, and health/readiness verification (FR26)
**And** `samples/deploy/azure/` contains deployment scripts or Bicep/ARM templates and DAPR component configs
**And** the guide explicitly references what the reader already knows and what's new (FR59)
**And** the guide explains infrastructure differences between local Docker, Kubernetes, and Azure (FR58)
**And** the guide includes resource requirements and scaling guidance (FR63)
**And** event data storage location is documented per backend (FR60)
**And** the walkthrough produces a verifiably running system (NFR22)
**And** the page follows the standard page template

### Story 7.4: Deployment Progression Guide

As a developer who started with the local Docker quickstart,
I want a guide showing the progression from local to Kubernetes to Azure,
So that I understand how the same application code runs across all environments with only infrastructure changes.

**Acceptance Criteria:**

**Given** all three deployment guides exist (Stories 7.1-7.3)
**When** a developer navigates to `docs/guides/deployment-progression.md`
**Then** the page shows a clear progression path: local Docker Compose -> on-premise Kubernetes -> Azure Container Apps using the same Counter application code
**And** the page highlights what changes between environments (DAPR components, infrastructure config) and what stays the same (application code)
**And** the page includes a comparison table of environment differences (FR58)
**And** the page links to each deployment guide as the detailed walkthrough
**And** the page follows the standard page template

### Story 7.5: DAPR Component Configuration Reference

As an operator configuring Hexalith for their infrastructure,
I want documented examples for configuring each DAPR component per backend,
So that I can set up State Store, Pub/Sub, Actors, Configuration, and Resiliency for my target environment.

**Acceptance Criteria:**

**Given** the deployment guides reference DAPR component configuration
**When** this story is complete
**Then** the deployment guides or a dedicated section document all five DAPR building blocks (State Store, Pub/Sub, Actors, Configuration, Resiliency) with configuration examples per backend
**And** each YAML example includes inline comments explaining every field
**And** examples cover at minimum: Redis (local dev), PostgreSQL (alternative), and Azure-managed services
**And** the content explains what persistence guarantees each backend provides (FR60)

### Story 7.6: Security Model Documentation

As an operator responsible for securing Hexalith,
I want to understand the security model and configure authentication,
So that I can protect the system in production.

**Acceptance Criteria:**

**Given** a developer navigates to `docs/guides/security-model.md`
**When** they read the page
**Then** the page explains: the authentication model (Keycloak integration in the sample), how to configure authentication for production, authorization model, and security boundaries between services
**And** the page follows the standard page template with DAPR explained at operational depth
**And** the page is self-contained (FR43)

### Story 7.7: Troubleshooting Guide

As a developer encountering errors,
I want a troubleshooting guide covering quickstart, DAPR integration, and deployment issues,
So that I can resolve problems without filing an issue.

**Acceptance Criteria:**

**Given** a developer encounters an error during quickstart, DAPR integration, or deployment
**When** they navigate to `docs/guides/troubleshooting.md`
**Then** the page covers quickstart errors: Docker not running, port conflicts, DAPR sidecar timeout, .NET SDK version mismatch, sample build failure (FR47)
**And** the page covers DAPR integration issues: sidecar injection failure, state store connection timeout, pub/sub message loss, actor activation conflict, component configuration mismatch (FR48)
**And** the page covers deployment failures per target environment: Docker Compose, Kubernetes, Azure (FR49)
**And** each issue includes: symptom description, probable cause, and step-by-step resolution
**And** the page follows the standard page template

### Story 7.8: Disaster Recovery Procedure

As an operator responsible for data integrity,
I want a documented disaster recovery procedure for the event store,
So that I can recover from data loss scenarios.

**Acceptance Criteria:**

**Given** an operator navigates to a disaster recovery section (in `docs/guides/troubleshooting.md` or a dedicated page)
**When** they read the content
**Then** the content documents: backup strategies per DAPR state store backend, recovery steps, data verification procedures, and RTO/RPO considerations
**And** the content is specific to the DAPR state store backends documented (Redis, PostgreSQL, Azure services)

## Epic 8: Configuration, Versioning & Lifecycle

Developers can access a complete configuration reference, understand event versioning, follow upgrade paths between versions, and view the product roadmap.

### Story 8.1: Configuration Reference

As a developer tuning Hexalith for their environment,
I want a complete configuration reference for all system knobs,
So that I can understand and adjust every configurable setting.

**Acceptance Criteria:**

**Given** a developer navigates to `docs/guides/configuration-reference.md`
**When** they read the page
**Then** the page documents every configurable setting: environment variables, DAPR component fields, Aspire configuration, application settings
**And** each setting includes: name, description, default value, valid values, and an example
**And** settings are organized by category (application, DAPR, infrastructure)
**And** the page follows the standard page template
**And** the page is self-contained (FR43)

### Story 8.2: Auto-Generated API Reference & CI Workflow

As a developer browsing the public API,
I want auto-generated API documentation for all public types,
So that I can look up type signatures, method parameters, and XML doc comments.

**Acceptance Criteria:**

**Given** the source code has XML documentation comments on public types
**When** this story is complete
**Then** `docs/reference/api/` contains auto-generated API documentation produced by DefaultDocumentation
**And** a curated `docs/reference/api/index.md` provides navigable entry points (not just a raw file tree)
**And** `.github/workflows/docs-api-reference.yml` exists and triggers on release tags only
**And** the workflow builds the solution with DefaultDocumentation, commits generated files to `docs/reference/api/`, and creates a PR with the changes
**And** the generated docs render correctly on GitHub
**And** NuGet package descriptions contain at least 3 keywords from the SEO keyword list (NFR28)

### Story 8.3: Event Versioning & Schema Evolution Guide

As a developer evolving their domain model,
I want to understand how event versioning and schema evolution are handled,
So that I can safely change event structures without breaking existing data.

**Acceptance Criteria:**

**Given** a developer navigates to a versioning section (in concepts or guides)
**When** they read the content
**Then** the content explains: how Hexalith handles event schema changes, strategies for upcasting/downcasting events, backward compatibility guarantees, and what happens when event structures change
**And** examples use the Counter domain to show a concrete versioning scenario
**And** the page follows the standard page template

### Story 8.4: Upgrade Path Documentation

As a developer upgrading between Hexalith versions,
I want a documented upgrade path with migration steps,
So that I can move between major versions safely.

**Acceptance Criteria:**

**Given** a developer needs to upgrade from one version to another
**When** they consult the upgrade documentation
**Then** the CHANGELOG (Story 1.6) documents breaking changes per release with migration steps
**And** a dedicated section or page explains the general upgrade procedure: check CHANGELOG, update NuGet packages, run tests, handle breaking changes
**And** the content links to the CHANGELOG for version-specific details

### Story 8.5: Public Product Roadmap

As a developer evaluating Hexalith's future direction,
I want to view the public product roadmap,
So that I can understand what's planned and assess the project's trajectory.

**Acceptance Criteria:**

**Given** a developer navigates to `docs/community/roadmap.md`
**When** they read the page
**Then** the page shows planned features and milestones organized by timeframe or priority
**And** the page includes a link to GitHub Issues/Milestones for real-time tracking
**And** the page follows the standard page template
**And** the README links to the roadmap

### Story 8.6: DAPR FAQ Deep Dive

As a developer with concerns about the DAPR dependency,
I want a comprehensive FAQ addressing DAPR-specific questions and risks,
So that I can make an informed decision about adopting Hexalith.

**Acceptance Criteria:**

**Given** a developer navigates to `docs/guides/dapr-faq.md`
**When** they read the page
**Then** the page provides deep, honest answers to: What if DAPR is deprecated? How does DAPR versioning affect Hexalith? What's the performance overhead of DAPR sidecars? Can I use Hexalith without DAPR? What are the operational costs of running DAPR?
**And** DAPR is explained at deep depth per the progressive explanation pattern — honest trade-off analysis, risk assessment, what-if scenarios
**And** the page follows the standard page template
**And** the page is self-contained (FR43)
