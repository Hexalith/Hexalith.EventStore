---
stepsCompleted:
  - step-01-init
  - step-02-discovery
  - step-02b-vision
  - step-02c-executive-summary
  - step-03-success
  - step-04-journeys
  - step-05-domain
  - step-06-innovation
  - step-07-project-type
  - step-08-scoping
  - step-09-functional
  - step-10-nonfunctional
  - step-11-polish
  - step-12-complete
  - edit-2026-02-24
inputDocuments:
  - prd.md
  - product-brief-Hexalith.EventStore-2026-02-11.md
workflowType: 'prd'
documentCounts:
  briefs: 1
  research: 0
  brainstorming: 0
  projectDocs: 1
classification:
  projectType: 'Developer Experience Product + Community Infrastructure'
  domain: 'Open-Source Developer Advocacy & Technical Marketing'
  complexity: 'High'
  complexityDrivers:
    - Three distinct audiences (evaluator, operator, architect) with different needs
    - Competitive positioning against established players (Marten, EventStoreDB, Axon)
    - Distributed systems concepts must balance accessibility with technical accuracy
    - Community infrastructure built from zero (trust signals, not just docs)
    - DAPR dependency requires proactive objection-handling
    - Multiple deployment scenarios (on-prem, cloud, hybrid) to document
    - Non-linear navigation as first-class requirement (every page is a landing page)
    - Documentation quality gates and testing strategy in scope
  projectContext: 'Brownfield product, greenfield documentation'
  scopeStrategy: 'Developer funnel - Hook > Try > Build > Trust > Stay > Contribute'
  innovationOpportunity: 'Interactive decision guide (no competitor does this)'
  coreRequirements:
    - Narrative positioning - origin story and developer empathy in README
    - Non-linear navigation - every page standalone + gateway into funnel
    - Community infrastructure - parallel workstream (issue templates, PR templates, CONTRIBUTING, CoC, Discussions, CI badges)
    - Decision guide - self-service qualification tool for evaluators
    - Documentation testing - code sample compilation, quickstart CI timing, link checking
editHistory:
  - date: '2026-02-24'
    changes: 'Post-validation edits: fixed 12 FR/NFR measurability issues, removed 2 implementation leakage violations, added Journey 5 (Marco Returns) for lifecycle traceability, added FR63 (resource sizing), assigned troubleshooting and CHANGELOG to phase deliverables, rewrote FR54 to align with latest-only exclusion'
  - date: '2026-02-24'
    changes: 'Post-revalidation polish: consolidated NFR12/NFR18 overlap (NFR12 now references NFR18 as quality gate), generalized NFR20 markdownlint tool name, added Phase-to-FR cross-reference table'
---

# Product Requirements Document - Hexalith.EventStore Documentation

**Author:** Jerome
**Date:** 2026-02-24

## Executive Summary

Hexalith.EventStore's documentation initiative is a developer experience product designed to drive open-source adoption of a DAPR-native, distributed event sourcing server for .NET. The documentation targets .NET developers evaluating, adopting, and deploying Hexalith.EventStore as an alternative to EventStoreDB, Marten, or custom-built event sourcing implementations. The core documentation strategy is progressive disclosure: lead with the pure function programming model `(Command, CurrentState?) -> List<DomainEvent>`, get developers running via quickstarts in under 10 minutes, and layer in distributed systems complexity only when developers need it.

Two parallel workstreams: (1) developer-facing documentation structured as an adoption funnel (Hook > Try > Build > Trust > Stay > Contribute), and (2) community infrastructure establishing trust signals for open-source credibility (issue templates, PR templates, CONTRIBUTING guide, code of conduct, GitHub Discussions, CI badges). Primary audience: .NET developers who understand DDD/CQRS concepts and are seeking an infrastructure-portable, multi-tenant event sourcing backbone that doesn't lock them into a specific database or message broker.

### What Makes This Special

The documentation strategy inverts the typical open-source approach of "explain the architecture, then show how to use it." Instead, it leads with the simplest possible entry point — one pure function contract — and makes the underlying DAPR infrastructure a feature to discover, not a barrier to entry. This progressive disclosure model matches how developers actually evaluate tools: run it first, understand it later.

Three elements differentiate this documentation from competitors: (1) an interactive decision guide that helps developers self-qualify whether Hexalith fits their needs — no competitor in the .NET event sourcing space offers this; (2) honest competitive positioning including "when NOT to use this" guidance, building trust through transparency; (3) deployment scenario documentation (on-premise, cloud, hybrid) that exploits Hexalith's core differentiator of infrastructure portability — a gap no competitor fills. Documentation quality is enforced through automated testing: code sample compilation validation, quickstart timing CI, and link checking.

## Project Classification

- **Project Type:** Developer Experience Product + Community Infrastructure
- **Domain:** Open-Source Developer Advocacy & Technical Marketing
- **Complexity:** High — three distinct audiences (evaluator, operator, architect), competitive positioning against established players, distributed systems concepts requiring accessible explanation, community built from zero, DAPR dependency requiring proactive objection-handling, multiple deployment scenarios, non-linear navigation as first-class requirement, documentation testing in scope
- **Project Context:** Brownfield product (Hexalith.EventStore exists with complete PRD and implementation), greenfield documentation (currently a single-line README with no docs, no community infrastructure)

## Success Criteria

### User Success

**Evaluator success (Hook stage):**
- A .NET developer landing on the README understands what Hexalith.EventStore does and whether it's relevant to them within 30 seconds
- The pure function contract `(Command, CurrentState?) -> List<DomainEvent>` is visible within the first screen scroll
- A developer can self-qualify via the decision guide without reading architecture docs

**Adopter success (Try + Build stages):**
- A developer completes the quickstart and has events flowing locally in under 10 minutes via `dotnet aspire run`
- A developer builds and registers their first custom domain service within 1 hour using the tutorial
- Zero "how do I get started?" issues filed on GitHub — the docs answer this completely

**Operator success (Stay stage):**
- A DevOps engineer can deploy to their target environment (Docker Compose, Kubernetes, or Azure Container Apps) using deployment guides without external help
- Infrastructure backend swap (e.g., Redis to PostgreSQL) documented as a single DAPR config change with copy-paste examples

### Business Success

**Primary metric:** 500 GitHub stars within 6 months of open-source publication

**Supporting metrics (6-month targets):**
- NuGet package downloads: 1,000+ (indicates actual usage beyond starring)
- GitHub forks: 50+ (indicates developers exploring the code)
- Quickstart completion rate: >80% of developers who clone the repo successfully run the system (measured by issue absence, not telemetry)
- External contributors: 3+ PRs from non-core team (docs or code)
- GitHub Discussions: Active community with <48hr response time on questions

**12-month targets:**
- 1,500+ stars
- Recognition in .NET event sourcing conversations (blog mentions, conference talks, Stack Overflow references)
- At least 1 production deployment by an external team

### Technical Success

**Documentation quality gates:**
- 100% of code examples in docs compile and run against the current release (validated by CI)
- Quickstart CI job completes in under 10 minutes on a clean environment
- Zero broken links across all documentation pages (validated by CI)
- All docs pages pass a readability threshold (no page requires more than 2 prerequisites to understand)

**Coverage completeness:**
- Every NuGet package (`Contracts`, `Client`, `Server`, `Aspire`, `Testing`) has dedicated API documentation
- Every deployment target (Docker Compose, Kubernetes, Azure Container Apps) has a tested walkthrough
- Every DAPR building block dependency (Actors, State Store, Pub/Sub, Config, Resiliency) has an explanation accessible to developers unfamiliar with DAPR

### Measurable Outcomes

| Metric | 3-month target | 6-month target | Measurement |
|--------|---------------|----------------|-------------|
| GitHub stars | 150+ | 500+ | GitHub API |
| NuGet downloads | 300+ | 1,000+ | NuGet stats |
| "Can't figure out" issues | <5 | <10 cumulative | GitHub issue triage |
| Quickstart success (no issues) | Zero blockers | Zero blockers | Issue tracker |
| Docs code samples passing CI | 100% | 100% | CI pipeline |
| External PRs | 1+ | 3+ | GitHub PR count |

## Product Scope

Every deliverable maps to a stage in the developer adoption funnel (Hook > Try > Build > Trust > Stay > Contribute). The scope is organized into phases below, each targeting a specific conversion goal.

**Funnel Stage Mapping:**

| Funnel Stage | Goal | Phase |
|-------------|------|-------|
| Hook | Convert browsers to readers | Phase 1a (Launch) |
| Try | Convert readers to users | Phase 1a (Launch) |
| Build | Convert users to builders | Phase 1b (Depth) |
| Trust + Stay | Convert builders to advocates | Phase 2 (Operations) |
| Contribute | Convert advocates to contributors | Phase 1a + Phase 2 |

Detailed deliverables per phase defined in [Project Scoping & Phased Development](#project-scoping--phased-development) below.

### Explicit Exclusions

The following are deliberately out of scope for all phases unless explicitly added via PRD amendment:

- **Versioned documentation** — latest-only; no version branches or version selector
- **Multilingual documentation** — English only (French deferred to Vision/Phase 4)
- **PDF/offline documentation generation** — GitHub-rendered markdown only
- **Custom documentation site for MVP** — GitHub repo browsing; docs site deferred to Phase 3
- **Video production** — deferred to Phase 3; MVP is text + GIF only
- **API versioning documentation** — single API version; versioning docs added when API v2 ships
- **Third-party integration guides** — no guides for specific ORMs, message brokers, or CI platforms beyond what DAPR abstracts
- **Paid/premium documentation tiers** — all documentation is MIT-licensed and free

## User Journeys

### Journey 1: "Marco Evaluates" — The Evaluator (Primary)

**Marco**, senior .NET developer at a mid-size SaaS company. He's spent the last two weeks building a custom event sourcing pipeline on top of Marten for a new multi-tenant product. He's frustrated — Marten locks him to PostgreSQL, but his team wants Cosmos DB for their Azure deployment. He's Googling "event sourcing .NET infrastructure agnostic" on a Thursday evening.

**Opening Scene:** Marco lands on the Hexalith.EventStore GitHub repo from a search result. He sees the star count, the last commit date, and the README. He's skeptical — he's seen dozens of abandoned .NET event sourcing projects. He gives it 30 seconds.

**Rising Action:** The README opens with a line that stops him: *"If you've spent weeks wiring up an event store, a message broker, and multi-tenant isolation — only to realize you'll do it again for your next project — we built this for you."* He feels seen. He scrolls. He sees the pure function contract: `(Command, CurrentState?) -> List<DomainEvent>`. That's it? That's all a domain service needs to implement? He sees the architecture diagram — DAPR handling actors, state store, pub/sub. He sees the "Why Hexalith?" comparison table: Hexalith vs Marten vs EventStoreDB. The row that catches his eye: "Infrastructure portability — swap Redis for PostgreSQL for Cosmos DB with zero code changes." That's exactly his problem.

**Climax:** Marco clicks the "Quickstart" link. He reads: "Prerequisites: .NET 10 SDK, Docker Desktop. Time: 10 minutes." He thinks: "I'll try it before dinner." He clones, runs `dotnet aspire run`, and watches the Aspire dashboard light up — EventStore server, sample domain service, Redis, RabbitMQ, all running. He sends a test command via the sample's REST endpoint. He watches the event flow through the dashboard. It took 8 minutes. He stars the repo.

**Resolution:** Marco bookmarks the "Build Your First Domain Service" tutorial for tomorrow. He shares the repo link in his team's Slack channel with the message: "Found this — might solve our Cosmos DB problem. Check the quickstart." Two more stars follow that evening.

**This journey reveals requirements for:**
- README with narrative hook, code snippet, architecture diagram, comparison table
- Quickstart link prominent and above the fold
- "Why Hexalith?" comparison page
- Sub-10-minute quickstart with minimal prerequisites
- Aspire dashboard as the visual "wow" moment

---

### Journey 2: "Marco Builds" — The Quickstarter Becomes a Builder

**Continuing Marco's story.** It's Friday morning. Marco has 2 hours before standup. He opens the "Build Your First Domain Service" tutorial.

**Opening Scene:** The tutorial starts with what Marco already understands: the pure function contract. It shows a simple inventory domain — `CreateProduct` command in, `ProductCreated` event out. No DAPR knowledge required. No infrastructure code.

**Rising Action:** Marco follows step by step. He creates a new .NET project, adds the `Hexalith.EventStore.Client` NuGet package, implements his domain processor as a pure function. The tutorial shows him how to register his service with EventStore via DAPR config. He runs the system — his new domain service appears in the Aspire dashboard alongside the sample.

**Climax:** Marco sends a `CreateProduct` command via curl. He watches his domain service receive it, process it, and return a `ProductCreated` event. The event appears in the event stream. He swaps the state store config from Redis to PostgreSQL with a single YAML change. Reruns. Same result. Zero code changes. He thinks: "This actually works."

**Resolution:** Marco spends the next hour exploring the Command API reference and event envelope schema. He starts sketching how to migrate his team's current Marten-based aggregate to Hexalith. He opens a GitHub Discussion: "Migrating from Marten — any gotchas?" He gets a response from the maintainer within hours.

**This journey reveals requirements for:**
- Step-by-step "Build your first domain service" tutorial
- NuGet package installation guide
- DAPR config registration walkthrough
- Infrastructure swap demonstration (Redis to PostgreSQL)
- Command API reference with curl examples
- Active GitHub Discussions with responsive maintainer presence

---

### Journey 3: "Priya Deploys" — The Architect/Operator

**Priya**, DevOps lead at Marco's company. Marco pitched Hexalith to the team. Priya's job: figure out if this can run in their Azure Kubernetes cluster and whether it's production-ready.

**Opening Scene:** Priya skips the README and goes straight to the docs folder. She searches for "Kubernetes" and "deployment." She finds a dedicated deployment guide for AKS.

**Rising Action:** The deployment guide lists exact prerequisites: DAPR runtime version, Helm chart for DAPR, resource requirements per pod. It shows the Aspire publisher command to generate Kubernetes manifests. It documents the DAPR component configs for Azure Cosmos DB (state store) and Azure Service Bus (pub/sub). Each config is a copy-paste YAML block with comments explaining every field.

**Climax:** Priya runs the Aspire publisher, gets Kubernetes manifests, applies them to a test cluster. The system comes up. She checks the health and readiness endpoints. She reads the security model docs — JWT auth, multi-tenant isolation layers, DAPR access control policies. She finds the "DAPR dependency FAQ" and reads the honest answer about what happens if DAPR changes direction. She thinks: "These people are transparent. I can work with this."

**Resolution:** Priya writes a technical assessment for her team: "Production-viable. Deployment path is clear. DAPR dependency is a calculated trade-off, well-documented. Recommend proceeding with a pilot."

**This journey reveals requirements for:**
- Deployment guides per target (Docker Compose, Kubernetes/AKS, Azure Container Apps)
- DAPR component config examples with copy-paste YAML
- Resource requirements and sizing guidance
- Health/readiness endpoint documentation
- Security model deep-dive
- DAPR dependency FAQ with honest risk assessment

---

### Journey 4: "Kenji Contributes" — The Contributor

**Kenji**, .NET developer in Tokyo. He's been using Hexalith for 3 months. He found a typo in the deployment guide and wants to fix it. He also has an idea for a gRPC command API and wants to propose it.

**Opening Scene:** Kenji opens CONTRIBUTING.md. He finds clear instructions: fork, branch, PR process. He sees a "good first issues" label in the issue tracker. The typo fix is straightforward — he submits a PR in 15 minutes.

**Rising Action:** For the gRPC proposal, Kenji opens a GitHub Discussion in the "Ideas" category. He outlines his proposal. Other community members weigh in. The maintainer responds with context about the roadmap (gRPC is planned for v4) and suggests Kenji could start with an RFC.

**Climax:** Kenji's typo PR is merged within 24 hours with a thank-you comment. He's now a contributor. He starts the gRPC RFC, knowing his work aligns with the roadmap.

**Resolution:** Kenji becomes a regular contributor. He writes a blog post in Japanese about his experience with Hexalith, bringing attention from the Japanese .NET community.

**This journey reveals requirements for:**
- CONTRIBUTING.md with clear fork/branch/PR workflow
- "Good first issues" label strategy
- Issue templates (bug, feature, docs improvement)
- PR template with checklist
- GitHub Discussions with category structure (Q&A, Ideas, Show & Tell)
- Responsive maintainer engagement (<24hr for PRs, <48hr for discussions)

---

### Journey 5: "Marco Returns" — The Returning User

**Marco**, 4 months after initial adoption. His team has been running Hexalith in production. A new major version is released with breaking changes to the event envelope schema.

**Opening Scene:** Marco sees the release notification. He opens the GitHub repo. He clicks CHANGELOG.md and sees a clear list of breaking changes with migration steps. The breaking change that affects his team — event envelope schema v2 — has a dedicated section with before/after examples.

**Rising Action:** Marco follows the upgrade path documentation. It tells him which NuGet packages to update, what code changes are needed, and how to handle existing event streams. He reads the event versioning guide and learns about the upcasting strategy Hexalith uses — old events are automatically upcast to the new schema on read.

**Climax:** Marco upgrades his staging environment following the documented steps. The upgrade completes without data loss. Old events read correctly through the upcaster. He checks the documentation page footer — it confirms he's reading docs for the version he just deployed.

**Resolution:** Marco updates production. He also reviews the disaster recovery procedure Priya had bookmarked — the documented backup and restore process for their PostgreSQL state store gives him confidence. He posts in GitHub Discussions: "Smooth upgrade from v1 to v2. Docs were solid."

**This journey reveals requirements for:**
- CHANGELOG.md with breaking changes and migration steps per release
- Event versioning and schema evolution documentation
- Documented upgrade path between major versions
- Version reference in README linking to the corresponding release tag
- Disaster recovery procedure for at least one state store backend

---

### Journey Requirements Summary

| Journey | Funnel Stage | Key Documentation Required | Star Impact |
|---------|-------------|---------------------------|-------------|
| Marco Evaluates | Hook | README, comparison page, decision guide, quickstart link | Direct — this is where stars happen |
| Marco Builds | Try + Build | Quickstart, tutorial, API reference, NuGet guide | Indirect — converts evaluators to advocates who share |
| Priya Deploys | Trust + Stay | Deployment guides, security docs, DAPR FAQ | Indirect — enables enterprise adoption and credibility |
| Kenji Contributes | Contribute | CONTRIBUTING, issue templates, Discussions | Long-term — grows community and content |
| Marco Returns | Stay | CHANGELOG, upgrade path, event versioning, DR procedure | Retention — keeps adopters through version transitions |

**Critical insight:** Journey 1 (Marco Evaluates) is the only journey that directly generates stars. Every other journey amplifies — Marco shares the repo after building, Priya's team stars after deploying, Kenji's blog brings new evaluators. But it all starts with the evaluator's 30-second decision.

## Domain-Specific Requirements

### Licensing & Legal

- MIT license clearly stated in README, docs, and every NuGet package
- Code examples in documentation are MIT-licensed — developers can copy-paste freely
- Third-party dependency licenses documented: DAPR (Apache 2.0), .NET Aspire (MIT), Blazor Fluent UI (MIT)

### SEO & Discoverability

README, NuGet package descriptions, and documentation filenames optimized for search visibility. Measurable SEO criteria defined in NFR24-28.

### Accessibility

Documentation meets structured accessibility standards (heading hierarchy, alt text, color independence, syntax highlighting). Measurable accessibility criteria defined in NFR6-10.

### Intellectual Property & Accuracy

- Competitor comparisons must be factually accurate and verifiable — no misrepresenting Marten, EventStoreDB, or Axon capabilities
- "When NOT to use this" section based on genuine technical limitations, not marketing spin
- All performance claims backed by reproducible benchmarks or explicitly labeled as targets

## Innovation & Novel Patterns

### Detected Innovation Areas

**1. Progressive Disclosure Documentation Architecture (Primary Innovation)**

Treating documentation structure as a UX design problem, not an information architecture problem. The traditional approach (architecture overview > concepts > getting started > reference) optimizes for completeness. The progressive disclosure approach (one contract > quickstart > tutorial > architecture) optimizes for conversion. Each layer adds complexity only when the developer is ready. This is the structural decision that drives stars 1-300 — it makes the README exceptional and the quickstart feel effortless. Architecture overview linked directly from README as a parallel path for architects who want depth immediately.

**2. Radical Transparency — "Choose the Right Tool" (Trust Innovation)**

Proactively documenting scenarios where Hexalith is the wrong choice, framed as helping developers choose the right tool — not as self-deprecation. If your project is a single-tenant app on PostgreSQL that will never change databases, Marten is simpler. If you need a dedicated event database with projections, EventStoreDB is more mature. Every limitation paired with the corresponding strength. This builds deep trust with serious evaluators and is frequently shared — "look at this project that actually helps you decide."

**3. Animated README Demo (Conversion Innovation)**

A 30-second animated GIF at the top of the README showing the complete quickstart in action: clone → `dotnet aspire run` → send command → watch event flow in Aspire dashboard. Before any text, developers see it working. GIFs are the #1 shared README element in open-source and the highest single-item star driver. This is the "show, don't tell" embodiment of the progressive disclosure philosophy.

**4. Experiential Comparison (Differentiation Innovation)**

At the end of the quickstart, a section showing: "Here's the same operation you just did — now see how it would look in Marten and EventStoreDB." The comparison becomes experiential, not theoretical. The developer just felt the Hexalith experience; now they can see the contrast with alternatives. This converts evaluators through hands-on proof rather than marketing claims.

**5. Tutorial-as-Test-Suite (Quality Innovation)**

The quickstart and tutorial steps ARE the integration test suite. If the tutorial runs, the docs are accurate. If a test breaks, the tutorial is stale. This eliminates the stale documentation problem at its root and makes docs CI trivial — just run the tutorials. Code sample compilation, quickstart timing, and link checking become byproducts, not separate infrastructure.

**6. "Awesome Event Sourcing" Curated Page (Community Innovation)**

A curated ecosystem page that positions Hexalith within the broader .NET event sourcing landscape. Links to competitors generously. Positions Jerome as a community leader, not just a project author. Drives SEO traffic from developers searching for event sourcing resources, who then discover Hexalith in context.

### Market Context & Competitive Landscape

| Innovation | Competitor Status | Market Gap | Star Impact |
|-----------|-------------------|------------|-------------|
| Progressive disclosure docs | Axon partially (guided tutorial) | Marten and EventStoreDB use traditional structure | Very High (drives stars 1-300) |
| "Choose the right tool" | None offer this | White space — unprecedented trust signal | High (shared frequently) |
| Animated README demo | Some large projects | No .NET event sourcing project does this | Very High (single highest-impact item) |
| Experiential comparison | None offer this | All comparisons are theoretical, not hands-on | High (converts evaluators) |
| Tutorial-as-test-suite | None at this scale | Novel approach to docs quality | Medium (indirect — prevents negative signals) |
| Awesome ecosystem page | Community-maintained lists exist | No project-authored ecosystem page | Medium (SEO + community positioning) |

### Validation Approach

| Innovation | Validation Method | Success Signal |
|-----------|-------------------|----------------|
| Progressive disclosure | Measure quickstart completion (issue absence) | <5 "how do I start?" issues in 6 months |
| Radical transparency | Track page views vs. star conversion rate | Evaluators who read "Choose the right tool" convert at higher rate |
| Animated README demo | Compare star rate before/after adding GIF | Measurable increase in stars per week |
| Experiential comparison | Track quickstart completion to star conversion | Higher star rate vs. quickstart without comparison section |
| Tutorial-as-test-suite | Monitor stale docs incidents | Zero stale code examples after any release |
| Awesome ecosystem page | Track inbound traffic from page | Measurable referral traffic to main repo |

### Risk Mitigation

| Innovation Risk | Fallback Strategy |
|----------------|-------------------|
| Progressive disclosure leaves architects stranded | Architecture overview linked directly from README as parallel path — "Ready to go deeper? Architecture overview →" |
| "Choose the right tool" misread as lack of confidence | Every limitation paired with corresponding strength; frame as informed decision-making, not self-deprecation |
| Animated GIF becomes outdated after UI changes | GIF generated from CI script (same as quickstart test); auto-regenerates on release |
| Experiential comparison feels like competitor bashing | Frame as educational, link to competitor docs respectfully, focus on trade-offs not winners/losers |
| Tutorial-as-test-suite is brittle | Separate tutorial CI from main build; failures flag docs team, don't block releases |
| Awesome ecosystem page becomes stale | Quarterly review cadence; community PRs welcome to update entries |

## Developer Documentation Specific Requirements

### Project-Type Overview

Hexalith.EventStore documentation is a developer-facing documentation product delivered as markdown files within the GitHub repository. It combines hand-crafted narrative content (README, guides, tutorials) with auto-generated API reference documentation. All code examples are extracted from runnable projects in the repo, enforcing accuracy through the tutorial-as-test-suite pattern. Documentation targets the latest release only — no version branching.

### Technical Architecture Considerations

**Documentation Stack:**

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| Content format | CommonMark markdown | Renders natively on GitHub, zero build step, PRs for changes |
| Diagrams | Mermaid | Renders in GitHub markdown, version-controlled, diff-friendly |
| API reference | Auto-generated from XML doc comments (DocFX) | Stays in sync with code automatically |
| Code examples | Extracted from runnable projects in `/samples` or `/docs/examples` | Tutorial-as-test-suite pattern — if it compiles and runs, the docs are accurate |
| Hosting | GitHub repository (`docs/` folder) for MVP | Zero infrastructure, discoverable via repo browsing |
| Search | GitHub's built-in search for MVP | Upgrade to docs site (Docusaurus/DocFX) in Growth phase |

**Documentation Site Structure:**

```
README.md                              # Hook — the front door
docs/
├── getting-started/
│   ├── prerequisites.md               # Exact versions, install commands
│   ├── quickstart.md                  # Clone to running in 10 minutes
│   └── first-domain-service.md        # Build tutorial (1 hour)
├── concepts/
│   ├── architecture-overview.md       # Topology diagrams, DAPR integration
│   ├── event-envelope.md              # 11-field metadata schema
│   ├── identity-scheme.md             # tenant:domain:aggregate-id
│   ├── command-lifecycle.md           # End-to-end flow
│   └── choose-the-right-tool.md       # "When NOT to use this"
├── guides/
│   ├── deployment-docker-compose.md   # On-premise / local
│   ├── deployment-kubernetes.md       # Cloud / AKS
│   ├── deployment-azure-container-apps.md  # Azure PaaS
│   ├── deployment-progression.md      # Same app: Docker → K8s → Azure (FR56)
│   ├── configuration-reference.md     # All knobs documented
│   ├── security-model.md             # JWT, multi-tenancy, DAPR policies
│   ├── troubleshooting.md            # Common errors, DAPR issues, deployment failures (FR47-49)
│   └── dapr-faq.md                   # DAPR dependency honest assessment
├── reference/
│   ├── command-api.md                 # REST endpoints, request/response
│   ├── nuget-packages.md             # Which package, when, why
│   └── api/                           # Auto-generated from XML docs (DocFX)
├── community/
│   ├── awesome-event-sourcing.md      # Curated ecosystem page
│   └── roadmap.md                     # Public roadmap
CONTRIBUTING.md                        # Contribution guide
CODE_OF_CONDUCT.md                     # Community standards
```

**Mermaid Diagram Requirements:**

Minimum diagrams needed for MVP:
- Architecture topology (EventStore server, DAPR sidecar, domain services, state store, pub/sub)
- Command lifecycle flow (API → actor → domain service → event persist → pub/sub)
- Identity scheme visualization (tenant:domain:aggregate-id → actor, stream, topic)
- Deployment topology per target (Docker Compose, Kubernetes, Azure Container Apps)
- NuGet package dependency graph (which package depends on which)

**Code Example Strategy:**

- All code examples live in runnable projects under a dedicated folder (e.g., `samples/` or `docs/examples/`)
- Docs reference code via file paths or code fences extracted from these projects
- CI runs these projects as integration tests — if the sample breaks, the docs are flagged stale
- Examples progress in complexity matching the funnel: quickstart (simplest) → tutorial (moderate) → advanced patterns (complex)

**API Reference Generation:**

- XML doc comments on all public types in `Hexalith.EventStore.Contracts` and `Hexalith.EventStore.Client`
- DocFX or similar tool generates API reference from XML docs
- Auto-generation runs as part of CI on each release
- Hand-written "NuGet packages guide" (`nuget-packages.md`) provides narrative context that auto-generated docs cannot

### Implementation Considerations

**Sample Application Definition:**
The sample application referenced throughout this PRD (FR7, FR22-24, FR56) is a multi-tenant inventory domain service demonstrating the core Hexalith programming model: `CreateProduct` command → `ProductCreated` event. It must demonstrate multi-tenancy (Hexalith's core differentiator) with at least two tenants visible in the Aspire dashboard. The same sample is used across all three deployment targets (Docker, Kubernetes, Azure) with only infrastructure configuration changes — proving the portability claim experientially.

**Content Authoring Workflow:**
- All docs changes go through PR review (same as code)
- Markdown linting via markdownlint in CI
- Link checking via automated tool in CI (lychee or similar)
- Code sample validation: CI builds and runs all projects in `samples/`
- CI runs on multi-OS matrix (Windows, macOS, Linux) to validate cross-platform quickstart (NFR21)

**Animated GIF Generation:**
- Script that runs the quickstart, captures terminal + Aspire dashboard via screen recording
- Output: 30-second GIF showing clone → run → command → event flow
- Regenerated on each major release via CI or manual trigger
- Hosted in repo (`docs/assets/quickstart-demo.gif`) for GitHub rendering

**Maintenance Model:**
- Latest-only documentation — no version branches
- Breaking changes documented in CHANGELOG.md
- Deprecated features marked with callouts in affected docs
- Quarterly review of all docs for staleness (community PRs welcome)

## Project Scoping & Phased Development

### MVP Strategy & Philosophy

**MVP Approach:** Phased Launch MVP — ship the evaluator-converting documentation first (Hook + Try), then build depth while the repo is live. Stars compound — every week of delay is a week of missed compounding.

**Strategic Rationale:** The 500-star goal requires early momentum. A developer who stars today may share the repo next week. Waiting for 25 perfect pages means zero stars during the writing period. Ship the Hook, get feedback, iterate.

**Resource Requirements:** Solo author (Jerome) with AI assistance. No additional team needed for MVP. Contributors may help with Growth phase docs after launch.

### MVP Feature Set — Phase 1a: Launch (The Hook + Try)

The absolute minimum for going live. Supports Marco's Evaluator journey end-to-end:

| Deliverable | Funnel Stage | Priority | Effort |
|-------------|-------------|----------|--------|
| README.md (narrative hook, code snippet, GIF, comparison table, quickstart link) | Hook | Critical | High — multiple iterations |
| Animated GIF demo (MUST-HAVE — highest single-item star driver, do not defer) | Hook | Critical | Medium — tooling setup |
| `docs/getting-started/quickstart.md` | Try | Critical | Medium |
| `docs/getting-started/prerequisites.md` | Try | Critical | Low |
| `docs/concepts/choose-the-right-tool.md` | Hook | Critical | Medium |
| CONTRIBUTING.md | Contribute | High | Low |
| CODE_OF_CONDUCT.md | Contribute | High | Trivial |
| Issue templates (bug, feature, docs) | Contribute | High | Low |
| PR template | Contribute | High | Low |
| LICENSE (already exists) | Trust | Done | Done |
| CHANGELOG.md | Trust | High | Low |

**Phase 1a delivers:** A developer can find the repo, understand what it does in 30 seconds, run the quickstart in 10 minutes, and star. Contributors have a clear path. **11 deliverables, launch-ready.**

### MVP Feature Set — Phase 1b: Build Depth (first 4 weeks post-launch)

Ships while the repo is live and generating initial traction. **Priority tiers prevent burnout** — Tier 1 ships in weeks 1-2, Tier 2 in weeks 3-4. If energy is low, Tier 2 can slip to Phase 2 without impact on star trajectory.

| Deliverable | Funnel Stage | Priority Tier |
|-------------|-------------|---------------|
| `docs/getting-started/first-domain-service.md` | Build | Tier 1 — Critical |
| `docs/concepts/architecture-overview.md` (with Mermaid diagrams) | Trust | Tier 1 — Critical |
| `docs/reference/command-api.md` | Build | Tier 1 — Critical |
| `docs/concepts/event-envelope.md` | Build | Tier 2 |
| `docs/concepts/identity-scheme.md` | Build | Tier 2 |
| `docs/concepts/command-lifecycle.md` | Build | Tier 2 |
| `docs/reference/nuget-packages.md` | Build | Tier 2 |
| `docs/community/awesome-event-sourcing.md` | Hook (SEO) | Tier 2 |
| Experiential comparison section (end of quickstart) | Hook | Tier 2 |

**Phase 1b Tier 1 delivers:** Marco can build his first domain service. Architects can evaluate the architecture. **Phase 1b Tier 2 delivers:** SEO starts working. Full concept coverage complete.

### Post-MVP Features — Phase 2: Operations & Production (months 2-3)

| Deliverable | Funnel Stage |
|-------------|-------------|
| `docs/guides/deployment-docker-compose.md` | Stay |
| `docs/guides/deployment-kubernetes.md` | Stay |
| `docs/guides/deployment-azure-container-apps.md` | Stay |
| `docs/guides/security-model.md` | Trust |
| `docs/guides/configuration-reference.md` | Stay |
| `docs/guides/dapr-faq.md` | Trust |
| DocFX API reference generation setup | Build |
| CI pipeline for docs validation (markdownlint, link checking, sample builds) | Infrastructure |
| `docs/community/roadmap.md` | Contribute |
| GitHub Discussions enabled with categories | Contribute |
| `docs/guides/troubleshooting.md` | Try + Stay |

**Phase 2 delivers:** Priya can deploy to production. Full trust layer complete. Documentation quality automated. Troubleshooting support in place.

### Post-MVP Features — Phase 3: Growth (months 4-6)

| Deliverable | Impact |
|-------------|--------|
| Searchable docs site (Docusaurus or DocFX hosted on GitHub Pages) | Discoverability |
| Video quickstart walkthrough | Conversion |
| Advanced tutorials (multi-tenant, custom backends) | Depth |
| Migration guides (from Marten, EventStoreDB) | Competitive |
| Performance benchmarks published | Trust |
| Blog-style "building Hexalith" series | Community |
| Tutorial-as-test-suite CI integration | Quality |

### Vision — Phase 4 (6+ months)

- Interactive playground (browser sandbox)
- AI-powered docs search
- Community showcase (adopter case studies)
- Conference talk kit
- Multilingual documentation (French)

### Risk Mitigation Strategy

**Technical Risks:**

| Risk | Impact | Mitigation |
|------|--------|------------|
| Animated GIF tooling complex to set up | Delays Phase 1a launch | GIF is must-have for star conversion — invest in tooling. If automated recording fails, use manual screen capture for v1 and automate in Phase 1b |
| DocFX integration heavy for solo dev | Delays API reference | Defer to Phase 2. Hand-written NuGet guide covers MVP |
| Mermaid diagrams time-consuming | Delays architecture docs | Phase 1b, not 1a. Start with 2 key diagrams (topology + command flow) |

**Market Risks:**

| Risk | Impact | Mitigation |
|------|--------|------------|
| Marten or EventStoreDB ships similar docs first | Reduced differentiation | Ship Phase 1a fast — first mover on "Choose the right tool" and animated GIF |
| Low initial traction despite good docs | Demoralizing, missed 500-star goal | Pair launch with Reddit r/dotnet post, HackerNews submission, Twitter/X thread. Docs enable sharing; marketing creates the initial spark |
| Negative early feedback on docs quality | Reputation damage | Soft launch (repo goes public) before hard launch (announcement posts). Iterate based on early visitors |

**Resource Risks:**

| Risk | Impact | Mitigation |
|------|--------|------------|
| Solo author burnout from 25-page scope | Stalled docs, abandoned repo | Phased approach — launch with 10 deliverables, not 25. Ship and iterate |
| Docs fall behind code changes | Stale examples, broken quickstart | Tutorial-as-test-suite (Phase 3) is the long-term fix. Short-term: quickstart is the only doc that MUST stay current |
| No contributors for Growth phase | Jerome writes everything alone | "Good first issues" label on docs improvements. Low-barrier contribution path attracts doc contributors |

## Functional Requirements

### Documentation Discovery & Evaluation

- **FR1:** A .NET developer can understand what Hexalith.EventStore does within 30 seconds of landing on the README
- **FR2:** A developer can see the core programming model (pure function contract) within the first screen scroll
- **FR3:** A developer can self-assess whether Hexalith fits their needs through a structured decision aid
- **FR4:** A developer can compare Hexalith's trade-offs against Marten, EventStoreDB, and custom implementations
- **FR5:** A developer can see a visual demonstration of the system running before installing anything
- **FR6:** A developer can identify all prerequisites needed before attempting the quickstart

### Getting Started & Onboarding

- **FR7:** A developer can clone the repository and have the sample application running with events flowing on a local Docker environment within 10 minutes
- **FR8:** A developer can follow step-by-step instructions to build and register a custom domain service
- **FR9:** A developer can experience an infrastructure backend swap (e.g., Redis to PostgreSQL) with zero code changes
- **FR10:** A developer can send a test command and observe the resulting event in the event stream

### Concept Understanding

- **FR11:** A developer can learn the architecture topology without prior DAPR knowledge
- **FR12:** A developer can understand the event envelope metadata structure
- **FR13:** A developer can understand the identity scheme and how it maps to actors, streams, and topics
- **FR14:** A developer can trace the end-to-end lifecycle of a command through the system
- **FR15:** A developer can understand why DAPR was chosen and what trade-offs it introduces
- **FR16:** A developer can understand when Hexalith is NOT the right choice for their project

### API & Technical Reference

- **FR17:** A developer can look up any REST endpoint with request/response examples
- **FR18:** A developer can determine which NuGet package to install for their use case
- **FR19:** A developer can browse auto-generated API documentation for all public types
- **FR20:** A developer can view the dependency relationships between NuGet packages
- **FR21:** A developer can access a complete configuration reference for all system knobs

### Deployment & Operations

- **FR22:** An operator can deploy the sample application to Docker Compose on a local development machine using a documented walkthrough
- **FR23:** An operator can deploy the sample application to an on-premise Kubernetes cluster using a documented walkthrough
- **FR24:** An operator can deploy the sample application to Azure Container Apps using a documented walkthrough
- **FR25:** An operator can configure each DAPR component (State Store, Pub/Sub, Actors, Configuration, Resiliency) for their target infrastructure with documented examples per backend
- **FR26:** An operator can verify system health through documented health/readiness endpoints
- **FR27:** An operator can understand the security model and configure authentication
- **FR57:** A developer can understand and set up the DAPR runtime for each target environment (local Docker, Kubernetes, Azure) as a prerequisite to deploying the sample application
- **FR58:** A developer can understand what infrastructure differences exist between local Docker, on-premise Kubernetes, and Azure cloud deployments and why each configuration differs
- **FR59:** A developer who completed the local Docker quickstart can transition to a Kubernetes or Azure deployment guide with explicit references to what they already know and what's new
- **FR60:** An operator can understand where event data is physically stored based on their DAPR state store configuration and what persistence guarantees each backend provides
- **FR63:** An operator can determine resource requirements (CPU, memory, storage) and pod sizing guidance for production deployment per target environment (Docker Compose, Kubernetes, Azure Container Apps)

### Community & Contribution

- **FR28:** A developer can find and follow a contribution workflow with documented fork, branch, and PR steps
- **FR29:** A developer can identify beginner-friendly contribution opportunities
- **FR30:** A developer can file structured bug reports, feature requests, and documentation improvements
- **FR31:** A developer can submit pull requests following a documented template and checklist
- **FR32:** A developer can participate in community discussions organized by category
- **FR33:** A developer can view the public product roadmap

### Content Quality & Maintenance

- **FR34:** A CI pipeline can validate that all code examples in documentation compile and run
- **FR35:** A CI pipeline can detect broken links across all documentation pages
- **FR36:** A CI pipeline can enforce markdown formatting standards
- **FR37:** Documentation maintainers can identify stale content through automated checks
- **FR38:** A documentation reviewer can verify changes through the same PR process as code
- **FR61:** A documentation contributor can run the full validation suite (code compilation, link checking, markdown linting) locally with a single command
- **FR62:** A maintainer can verify that every functional requirement has at least one corresponding documentation page through a traceability check

### SEO & Discoverability

- **FR39:** The README can be discovered through GitHub search for key terms (event sourcing, .NET, DAPR, multi-tenant)
- **FR40:** Documentation pages can be indexed by search engines with descriptive URLs and structured headings
- **FR41:** A developer browsing event sourcing resources can discover Hexalith through the curated ecosystem page
- **FR42:** A developer can navigate between related documentation pages through cross-linking

### Documentation Navigation & Structure

- **FR43:** A developer can enter the documentation at any page and orient themselves without reading prerequisite pages
- **FR44:** A developer can navigate a progressive complexity path from simple concepts to advanced patterns
- **FR45:** A developer can access architecture documentation directly from the README as a parallel entry point
- **FR46:** A developer can identify their current position in the documentation structure

### Troubleshooting & Error Handling

- **FR47:** A developer can find troubleshooting guidance for quickstart errors including: Docker not running, port conflicts, DAPR sidecar timeout, .NET SDK version mismatch, and sample build failure
- **FR48:** A developer can find documented solutions for DAPR integration issues including: sidecar injection failure, state store connection timeout, pub/sub message loss, actor activation conflict, and component configuration mismatch
- **FR49:** A developer can access troubleshooting information for deployment failures per target environment

### Lifecycle & Versioning

- **FR50:** A developer can view a changelog of breaking changes and migration steps between releases
- **FR51:** A developer can understand how event versioning and schema evolution are handled
- **FR52:** A developer can follow a documented upgrade path when moving between major versions
- **FR53:** A developer can set up a local development environment matching the documented configuration
- **FR54:** A developer can identify the library version documented by a documentation page via a version reference in the README linking to the corresponding release tag
- **FR55:** An operator can follow a documented disaster recovery procedure for the event store
- **FR56:** A developer can follow a documented progression from the local Docker sample to on-premise Kubernetes to Azure cloud deployment using the same application code with only infrastructure configuration changes

## Non-Functional Requirements

### Performance

- **NFR1:** The quickstart guide results in a running system within 10 minutes on a clean development machine with prerequisites installed (validated by CI timing)
- **NFR2:** Any documentation page renders fully on GitHub (all markdown content and Mermaid diagrams visible) within 2 seconds on a 25 Mbps connection, validated by page file size staying under 200KB
- **NFR3:** The animated README GIF is under 5MB in file size (validated by CI file size check)
- **NFR4:** Mermaid diagrams produce visible, non-error diagrams when viewed on GitHub.com in Chrome, Firefox, and Edge latest versions, without requiring external tooling or browser extensions
- **NFR5:** The "Build Your First Domain Service" tutorial completes within 1 hour for a developer familiar with .NET and DDD concepts, validated by a timed walkthrough during authoring and by tutorial CI job completion time

### Accessibility

- **NFR6:** All documentation pages use structured heading hierarchy (H1-H4) with no skipped levels, enabling screen reader navigation
- **NFR7:** All architecture diagrams and Mermaid visuals include descriptive alt text that conveys the same information as the visual
- **NFR8:** Color is never the sole indicator of meaning in any diagram, table, or callout — shape, label, or pattern must also distinguish elements
- **NFR9:** Code examples include language-specific syntax highlighting tags for assistive technology compatibility
- **NFR10:** No documentation page requires more than 2 prerequisite pages to understand (maximum depth of dependency)

### Maintainability

- **NFR11:** Documentation pages are self-contained markdown files with no cross-file build dependencies, enabling any single page to be updated and validated within one CI cycle
- **NFR12:** No code examples in documentation are manually maintained in markdown files — all are extracted from or validated against runnable source projects (see NFR18 for the compile-and-run quality gate)
- **NFR13:** Documentation changes follow the same PR review process as code changes, with no separate publishing workflow
- **NFR14:** Stale content is detectable within 1 CI cycle after a breaking code change
- **NFR15:** The animated README GIF can be regenerated via a single script or CI job, not manual screen recording
- **NFR16:** Adding a new documentation page requires no changes to build configuration or CI pipeline — drop a markdown file in the correct folder
- **NFR17:** Documentation structure supports a quarterly review cadence — each page has a clear owner (Jerome for MVP) and last-reviewed date is trackable via git history

### Reliability

- **NFR18:** 100% of code examples in documentation compile and run successfully against the current release (validated by CI on every commit)
- **NFR19:** Zero broken links across all documentation pages (validated by CI link checker on every commit)
- **NFR20:** Markdown formatting passes linting rules consistently (validated by a markdown linting tool in CI)
- **NFR21:** The quickstart produces identical results on macOS, Windows, and Linux development machines with Docker Desktop installed
- **NFR22:** All three deployment walkthroughs (Docker Compose, Kubernetes, Azure) produce a verifiably running system when followed step-by-step
- **NFR23:** CI validation pipeline completes in under 5 minutes to avoid blocking PR merges

### Discoverability & SEO

- **NFR24:** README contains all primary search keywords (event sourcing, .NET, DAPR, distributed, multi-tenant, event store, CQRS, DDD) within the first 200 words
- **NFR25:** Every documentation page starts with a clear H1 title and a one-paragraph summary optimized for search engine snippets
- **NFR26:** Documentation filenames use descriptive, unabbreviated words separated by hyphens (e.g., `deployment-kubernetes.md` not `deploy-k8s.md`)
- **NFR27:** Cross-links between related documentation pages ensure no page is more than 2 clicks from the README
- **NFR28:** NuGet package descriptions contain at least 3 keywords from the NFR24 keyword list (event sourcing, .NET, DAPR, distributed, multi-tenant, event store, CQRS, DDD) and use terminology consistent with the documentation

## Phase-to-FR Cross-Reference

Maps each phase to its constituent functional requirements for quick agent and reviewer lookup.

| Phase | FRs | Count |
|-------|-----|-------|
| Phase 1a (Launch) | FR1, FR2, FR3, FR4, FR5, FR6, FR7, FR10, FR28, FR29, FR30, FR31, FR39, FR40, FR41, FR42, FR43, FR44, FR45, FR46, FR50, FR53, FR54 | 23 |
| Phase 1b (Depth) | FR8, FR9, FR11, FR12, FR13, FR14, FR15, FR16, FR17, FR18, FR20 | 11 |
| Phase 2 (Operations) | FR19, FR21, FR22, FR23, FR24, FR25, FR26, FR27, FR32, FR33, FR34, FR35, FR36, FR37, FR38, FR47, FR48, FR49, FR51, FR52, FR55, FR56, FR57, FR58, FR59, FR60, FR61, FR62, FR63 | 29 |
