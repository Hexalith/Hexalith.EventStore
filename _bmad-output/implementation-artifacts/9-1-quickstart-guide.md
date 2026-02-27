# Story 9.1: Quickstart Guide

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a .NET developer,
I want step-by-step instructions to clone the repo and run the sample application with events flowing locally,
so that I can experience the system working within 10 minutes.

## Acceptance Criteria

1. **AC1 - End-to-End Quickstart Flow**: Given a developer has completed all prerequisites (Docker Desktop, .NET 10 SDK, DAPR CLI), when they follow `docs/getting-started/quickstart.md`, then the guide walks them through: clone the repo, run the sample via Aspire, send a test command to the Counter domain, and observe the resulting event in the event stream

2. **AC2 - 10-Minute Completion (NFR1)**: The guide completes in under 10 minutes on a clean machine with prerequisites installed

3. **AC3 - Cross-Platform (NFR21)**: The guide works identically on macOS, Windows, and Linux with Docker Desktop

4. **AC4 - DAPR Functional Explanation**: DAPR is explained at functional depth ("DAPR handles message delivery and state storage — you don't write infrastructure code") per the progressive explanation pattern

5. **AC5 - Page Template Compliance (D7)**: The page follows the standard page template: back-link to README (`[← Back to Hexalith.EventStore](../../README.md)`), H1 title, one-paragraph summary, prerequisites link to `prerequisites.md`, content sections, Next Steps footer

6. **AC6 - Counter Domain Names**: Inline code examples use the Counter domain names (`IncrementCounter`, `CounterProcessor`, `CounterState`)

7. **AC7 - Relative Cross-Links (FR42)**: Cross-links to related pages (prerequisites, architecture overview, choose-the-right-tool) use relative paths

8. **AC8 - Self-Contained (FR43)**: The page is self-contained — a developer arriving from search can orient themselves without reading other pages first

## Tasks / Subtasks

- [x] Task 1: Replace quickstart.md stub with page structure (AC: 5, 8)
  - [x] Remove placeholder content from existing `docs/getting-started/quickstart.md`
  - [x] Add back-link: `[← Back to Hexalith.EventStore](../../README.md)`
  - [x] Add H1 title: `# Quickstart`
  - [x] Add one-paragraph summary: what this guide covers (clone, run, send command, see events), who it's for (.NET developers), and time estimate (10 minutes)
  - [x] Add prerequisites callout: `> **Prerequisites:** [Prerequisites](prerequisites.md)`
  - [x] Ensure heading hierarchy: H1 → H2 → H3, no skipped levels

- [x] Task 2: Write "What You'll Build" section (AC: 1, 4, 6)
  - [x] Add H2 section: `## What You'll Build`
  - [x] Brief description of the Counter domain sample: `IncrementCounter` command → `CounterIncremented` event → `CounterState` updated
  - [x] Show the pure function contract concept: developer implements a function, the platform handles everything else
  - [x] DAPR functional explanation: "DAPR handles message delivery and state storage — you don't write infrastructure code"
  - [x] Keep to 3-5 sentences maximum — this is context, not a tutorial

- [x] Task 3: Write "Clone and Run" section (AC: 1, 2, 3)
  - [x] Add H2 section: `## Clone and Run`
  - [x] Step 1: Clone the repository — `git clone https://github.com/Hexalith/Hexalith.EventStore.git`
  - [x] Step 2: Navigate to the repo — `cd Hexalith.EventStore`
  - [x] Step 3: Start the Aspire AppHost — determine the correct command (likely `dotnet run --project src/Hexalith.EventStore.AppHost`)
  - [x] IMPORTANT: Verify the exact Aspire run command by reading `src/Hexalith.EventStore.AppHost/Program.cs` and project files — do NOT guess
  - [x] Describe expected output: Aspire dashboard URL (typically https://localhost:15888 or similar), services starting up
  - [x] Note: First run may take longer due to NuGet restore and Docker image pulls
  - [x] All terminal commands in `bash` code blocks with `$` prefix

- [x] Task 4: Write "Send a Command" section (AC: 1, 6)
  - [x] Add H2 section: `## Send a Command`
  - [x] Guide developer to find the Swagger UI URL from Aspire dashboard (CommandAPI service endpoint + `/swagger`)
  - [x] Show how to send an `IncrementCounter` command via Swagger UI "Try it out"
  - [x] CRITICAL: Determine the exact request payload by reading the CommandAPI endpoint code and `IncrementCounter` command type — do NOT invent payload shapes
  - [x] Read `src/Hexalith.EventStore.CommandApi/` controllers/endpoints to determine exact URL path (`/api/v1/commands` or similar)
  - [x] Read `samples/Hexalith.EventStore.Sample/Counter/Commands/IncrementCounter.cs` for command properties
  - [x] Show expected response: `202 Accepted` with correlation ID and Location header
  - [x] Show how to check command status via the status endpoint

- [x] Task 5: Write "See the Event" section (AC: 1, 6)
  - [x] Add H2 section: `## See the Event`
  - [x] Guide developer to observe the event in the Aspire dashboard traces
  - [x] Explain the trace shows: API received → Actor processing → Domain service invoked → Events stored → Events published
  - [x] Mention the correlation ID links the command to its resulting events
  - [x] Optionally show structured logs in Aspire dashboard

- [x] Task 6: Write "What Happened" explanation section (AC: 4, 6, 8)
  - [x] Add H2 section: `## What Happened`
  - [x] Brief walkthrough of the command lifecycle the developer just witnessed:
    1. You sent an `IncrementCounter` command via REST API
    2. The CommandAPI validated and routed it
    3. DAPR activated a `CounterProcessor` actor
    4. The actor loaded current `CounterState` and called the pure function
    5. The function produced a `CounterIncremented` event
    6. The event was persisted and published
  - [x] Reinforce: "You wrote zero infrastructure code — DAPR handled state, messaging, and actor lifecycle"
  - [x] Keep concise — this is the "aha moment", not a deep dive

- [x] Task 7: Write Next Steps footer (AC: 5, 7)
  - [x] Add H2 section: `## Next Steps`
  - [x] Primary next: link to first-domain-service tutorial (even if not yet written — use the expected path `first-domain-service.md`)
  - [x] Related links using relative paths:
    - `[Architecture Overview](../concepts/architecture-overview.md)` — understand the design decisions
    - `[Choose the Right Tool](../concepts/choose-the-right-tool.md)` — compare Hexalith with alternatives
    - `[Prerequisites](prerequisites.md)` — review tool setup
  - [x] All links relative, no absolute URLs

- [x] Task 8: Final validation (AC: all)
  - [x] Verify back-link to README uses correct relative path: `../../README.md`
  - [x] Verify heading hierarchy: H1 → H2 (no H3 expected unless subsections needed), no skipped levels
  - [x] Verify all code blocks have language tags (`bash` for terminal, `csharp` for C#, `json` for payloads)
  - [x] Verify no YAML frontmatter
  - [x] Verify all internal links use relative paths
  - [x] Verify no `[!NOTE]` GitHub alerts (use `> **Note:**` instead)
  - [x] Verify no emojis in the documentation page
  - [x] Verify page is self-contained — reads coherently for someone arriving from search
  - [x] Verify Counter domain names used consistently: `IncrementCounter`, `CounterProcessor`, `CounterState`, `CounterIncremented`
  - [x] Verify page size is under 200KB (NFR2)
  - [x] Read through as a developer unfamiliar with the project — does the 10-minute promise feel achievable?

## Dev Notes

### Architecture Source

This story implements **FR7** (10-minute quickstart), **FR10** (send command, observe event), **FR42** (cross-linking), and **FR43** (self-contained page) from `_bmad-output/planning-artifacts/prd-documentation.md`, following the page structure defined in **Decision D7** and folder structure from **Decision D1** in `_bmad-output/planning-artifacts/architecture-documentation.md`.

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

See `docs/page-template.md` for the full formatting reference.

### Technical Requirements

**Quickstart Page Suggested Structure:**

```markdown
[← Back to Hexalith.EventStore](../../README.md)

# Quickstart

One paragraph: clone to events flowing in 10 minutes, for .NET developers.

> **Prerequisites:** [Prerequisites](prerequisites.md)

## What You'll Build
(Counter domain intro, DAPR functional explanation)

## Clone and Run
(git clone, cd, dotnet run apphost, Aspire dashboard)

## Send a Command
(Swagger UI, IncrementCounter payload, 202 Accepted)

## See the Event
(Aspire traces, correlation ID, event lifecycle)

## What Happened
(Brief lifecycle walkthrough — the "aha moment")

## Next Steps
- **Next:** [Build Your First Domain Service](first-domain-service.md)
- **Related:** [Architecture Overview](../concepts/architecture-overview.md), [Choose the Right Tool](../concepts/choose-the-right-tool.md)
```

**Critical: Discover Actual Commands and Payloads from Source Code**

Do NOT guess or invent API payloads, URLs, or run commands. You MUST read these files to extract the real values:

| What to discover | Where to find it |
|-----------------|-----------------|
| Aspire run command | `src/Hexalith.EventStore.AppHost/` — read the `.csproj` and `Program.cs` to determine the correct `dotnet run` invocation |
| CommandAPI base URL and port | `src/Hexalith.EventStore.AppHost/Program.cs` — look for how `commandapi` resource is defined and what port it uses (likely 8080) |
| Command endpoint path | `src/Hexalith.EventStore.CommandApi/` — search for controller/endpoint definitions (likely `POST /api/v1/commands`) |
| IncrementCounter payload shape | `samples/Hexalith.EventStore.Sample/Counter/Commands/IncrementCounter.cs` — read the record type for required properties |
| CommandEnvelope wrapper | `src/Hexalith.EventStore.Contracts/` — read `CommandEnvelope` to understand how commands are wrapped (tenant ID, aggregate ID, etc.) |
| Command status endpoint | `src/Hexalith.EventStore.CommandApi/` — search for status/query endpoints |
| Aspire dashboard URL | Aspire default is `https://localhost:15888` but verify from AppHost output or configuration |
| Swagger UI path | Typically `/swagger` — verify from CommandAPI `Program.cs` OpenAPI setup |

**DAPR Explanation Depth — Functional Level ONLY:**

Per the progressive explanation pattern established across all documentation stories:

| Page | DAPR Depth |
|------|-----------|
| README | One sentence: "Built on DAPR for infrastructure portability" |
| Prerequisites | Installation-focused: what DAPR is, how to install it |
| **Quickstart (THIS PAGE)** | **Functional: "DAPR handles message delivery and state storage — you don't write infrastructure code"** |
| Concepts pages (future) | Architectural: which DAPR building blocks are used and why |
| DAPR FAQ (future) | Deep: trade-off analysis |

Do NOT explain DAPR architecture, building blocks, or sidecars on the quickstart page. Keep it to one functional sentence at the point where it matters (the "What You'll Build" or "What Happened" section).

### Architecture Compliance

**Content Format (D1):**
- CommonMark markdown — renders natively on GitHub, zero build step
- File location: `docs/getting-started/quickstart.md` (overwrite existing stub)
- File naming: lowercase, hyphen-separated per NFR26

**Markdown Standards (enforced by markdownlint-cli2):**
- NO YAML frontmatter (GitHub renders it as visible text)
- H1 = page title (one per page, never skip levels)
- H2 = major sections, H3 = subsections if needed
- Never skip heading levels (H1 → H3 without H2 is invalid)
- All code blocks MUST specify language: ` ```bash `, ` ```csharp `, ` ```json `, ` ```yaml `
- Callouts use blockquote syntax: `> **Note:**`, `> **Tip:**` — NEVER use `[!NOTE]` GitHub alerts
- No hard line wrapping in markdown source
- No emojis in the documentation page

**Cross-Linking (D7):**
- ALL internal links use relative paths — never absolute URLs for internal docs
- Same folder: `[link](file.md)` — e.g., `[Prerequisites](prerequisites.md)`
- Parent folder: `[link](../folder/file.md)` — e.g., `[Choose the Right Tool](../concepts/choose-the-right-tool.md)`
- Root: `[link](../../README.md)`
- External links: full URL with descriptive text — e.g., `[DAPR state store docs](https://docs.dapr.io/...)`
- Sample code references: relative link to file — e.g., `[CounterProcessor.cs](../../samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs)`

**Code Block Conventions:**
- Terminal commands: `bash` language tag with `$` prefix
- C# code: `csharp` language tag, always show required `using` statements
- JSON payloads: `json` language tag
- Variable names MUST match sample project: `CounterProcessor`, `CounterState`, `IncrementCounter`, `CounterIncremented` — never invent names like `MyProcessor` or `SampleHandler`
- Show happy path only — no try/catch or error handling in quickstart code
- Copy-pasteable: every code block should work if pasted directly

**Performance Constraints:**
- Page file size under 200KB (NFR2)
- Page renders on GitHub within 2 seconds on 25 Mbps (NFR2)
- Quickstart completion under 10 minutes (NFR1)

### Library & Framework Requirements

This story produces only a markdown documentation file — no code dependencies. However, the quickstart content MUST accurately reference the project's actual technology stack:

| Technology | Version | Source File |
|-----------|---------|-------------|
| .NET SDK | 10.0.102+ | `global.json` |
| Target Framework | net10.0 | `Directory.Build.props` |
| DAPR SDK | 1.16.1 | `Directory.Packages.props` |
| DAPR CLI | 1.16.x+ | Aligned with SDK |
| Aspire | 13.1.2 | `Directory.Packages.props` |
| Docker Desktop | Latest stable | DAPR requirement |

Do NOT hard-code specific patch versions in prose (they go stale). Use "10.0.102 or later" or link to the prerequisites page which already has version details.

### File Structure Requirements

**File to modify:**
- `docs/getting-started/quickstart.md` — complete rewrite from 12-line stub to full quickstart guide

**Files to read (DO NOT modify):**
- `docs/page-template.md` — formatting conventions and page structure rules
- `docs/getting-started/prerequisites.md` — to understand what's already covered (avoid duplication)
- `README.md` — to verify the quickstart link text and verify no contradictions
- `src/Hexalith.EventStore.AppHost/Program.cs` — to determine exact Aspire run command and service topology
- `src/Hexalith.EventStore.AppHost/*.csproj` — project file for run command
- `src/Hexalith.EventStore.CommandApi/` — controllers/endpoints for exact API paths and payload shapes
- `src/Hexalith.EventStore.Contracts/` — `CommandEnvelope` and related types for payload structure
- `samples/Hexalith.EventStore.Sample/Counter/Commands/IncrementCounter.cs` — command record type
- `samples/Hexalith.EventStore.Sample/Counter/Events/CounterIncremented.cs` — event record type
- `samples/Hexalith.EventStore.Sample/Counter/State/CounterState.cs` — state record type
- `samples/Hexalith.EventStore.Sample/Counter/CounterProcessor.cs` — the pure function implementation

**Files that should NOT be created or modified:**
- No new files — this story modifies only `docs/getting-started/quickstart.md`
- Do NOT modify the sample application, AppHost, or any source code
- Do NOT create test files (integration test project is a separate story: Epic 6, Story 6-1)

**Alignment with existing docs structure:**
```
docs/
├── getting-started/
│   ├── prerequisites.md        ← DONE (Story 8-3) — links TO quickstart
│   └── quickstart.md           ← THIS FILE — links FROM prerequisites
├── concepts/
│   ├── architecture-overview.md  ← Stub (Story 12-1 future) — link to it anyway
│   └── choose-the-right-tool.md  ← DONE (Story 8-4) — link to it
├── assets/
│   └── quickstart-demo.gif       ← DONE (Story 8-5) — reference if useful
└── page-template.md              ← DONE (Story 8-1) — follow its rules
```

### Testing Standards

This story produces a single markdown file. Validation checklist:

1. **Page template compliance**: back-link (`[← Back to Hexalith.EventStore](../../README.md)`), H1 title, summary paragraph, prerequisites callout, content sections, Next Steps footer
2. **Heading hierarchy**: H1 → H2 → H3, no skipped levels
3. **Code block language tags**: every code fence has `bash`, `csharp`, `json`, or `yaml` — no bare fences
4. **No YAML frontmatter**: first line is the back-link, not `---`
5. **Relative links**: all internal links use relative paths — verify each points to an existing file or known-stub
6. **No `[!NOTE]` alerts**: only `> **Note:**` blockquote syntax
7. **No emojis**: clean professional documentation
8. **Counter domain names**: `IncrementCounter`, `CounterProcessor`, `CounterState`, `CounterIncremented` used consistently — no invented names
9. **Self-contained**: read the page in isolation — does it make sense without having read prerequisites first?
10. **API accuracy**: every URL path, payload shape, and response format matches what the actual running system produces — verified by reading source code
11. **Cross-platform**: no OS-specific commands without alternatives (or note when a command works on all platforms)
12. **Page size**: well under 200KB
13. **10-minute flow**: read through the steps — does the flow feel achievable in 10 minutes for someone with prerequisites installed?

### Previous Story Intelligence (Epic 8 — Documentation Foundation)

Story 9-1 is the first story in Epic 9, but it builds directly on the completed Epic 8 (Foundation & First Impression). All 6 Epic 8 stories are done and provide critical context.

**Story 8-1 (Folder Structure & Page Conventions) — Learnings:**
- `docs/getting-started/` folder exists with real files — no `.gitkeep` remaining
- `docs/page-template.md` documents all formatting rules — use it as the definitive reference
- File naming: lowercase, hyphen-separated, descriptive (e.g., `quickstart.md` not `quick-start-guide.md`)
- `.gitkeep` files are removed once real content exists in a folder

**Story 8-2 (README Rewrite) — Learnings:**
- README promises "Get started in under 10 minutes" and links to `docs/getting-started/quickstart.md` — the quickstart MUST deliver on this promise
- README shows the pure function contract with `CounterProcessor` example — quickstart should reinforce this, not contradict it
- README has a prominent "Quickstart" link above the fold — the file path must match exactly
- Viewport constraint: first 6 sections should fit ~49 lines for first-scroll readability
- SEO keywords in first 200 words: "event sourcing", ".NET", "DAPR", "distributed", "multi-tenant"

**Story 8-3 (Prerequisites Page) — Learnings:**
- Prerequisites page at `docs/getting-started/prerequisites.md` is COMPLETE and covers:
  - .NET 10 SDK (10.0.102+), Docker Desktop (with WSL 2 note), DAPR CLI (1.16.x+)
  - Verification commands for each tool
  - `dapr init` instructions with expected containers (dapr_placement, dapr_redis, dapr_zipkin)
  - Common troubleshooting section
  - Next Steps links to `quickstart.md` as primary next step
- The quickstart should NOT duplicate any prerequisite content — just link to `prerequisites.md`
- DAPR explanation at prerequisites level = installation-focused. Quickstart level = functional.
- Review finding: replaced brittle version expectations with stable patterns (e.g., "output starts with Docker version" instead of "Docker version 27.x.x")

**Story 8-4 (Choose the Right Tool) — Learnings:**
- Decision aid at `docs/concepts/choose-the-right-tool.md` is COMPLETE
- Provides comparison with Marten, EventStoreDB, and Custom/DIY approaches
- Quickstart should link to this as a "Related" page in Next Steps for developers still evaluating

**Story 8-5 (GIF Demo) — Learnings:**
- Animated GIF at `docs/assets/quickstart-demo.gif` (2.1 MB) shows: Aspire dashboard → Swagger UI → command submission → event appearing
- The GIF already demonstrates the quickstart flow visually — the written guide should match this sequence
- Recording showed the actual Aspire AppHost topology: commandapi, sample service, redis, keycloak
- AppHost port: CommandAPI on 8080, Keycloak on 8180, Aspire dashboard typically on 15888 or dynamic

**Story 8-6 (CHANGELOG) — Learnings:**
- `CHANGELOG.md` is complete with full project history
- No specific impact on quickstart content

**Cross-Story Pattern: Content Voice and Tone (established across all Epic 8 stories):**
- Second person: "you", "your" — "Before you run the quickstart..."
- Professional-casual: developer-to-developer, not marketing or academic
- Active voice: "Clone the repository" not "The repository should be cloned"
- Assume reader knows .NET but may not know DAPR or Aspire
- No emojis in documentation pages

### Git Intelligence

**Recent commits (documentation initiative):**
```
7c4b9e8 Merge pull request #65 from Hexalith/feat/story-8-6-changelog-initialization
acf9e24 feat: Complete Story 8-6 CHANGELOG initialization with full project history
c7c0f46 Merge pull request #64 from Hexalith/feat/story-8-5-gif-capture-completion
656286c feat: Complete Story 8-5 GIF capture and address review findings
67d0b74 build(apphost): update UserSecretsId to descriptive string
3a3f15f feat: Update Aspire package versions to 13.1.2 in project files
22b09f8 feat: Complete Story 8-5 animated GIF demo capture and update README
52c45dd Merge pull request #63 from Hexalith/feat/story-8-4-choose-the-right-tool-decision-aid
f40d177 feat: Complete Story 8-4 Choose the Right Tool decision aid page
1450008 Merge pull request #62 from Hexalith/feat/story-8-3-prerequisites-page
```

**Patterns observed:**
- Branch naming: `feat/story-X-Y-description` (e.g., `feat/story-8-6-changelog-initialization`)
- Commit messages: conventional commits format — `feat:`, `build:`, `fix:`
- PRs: one per story, merged to main
- Stories implement sequentially within an epic
- Aspire packages recently updated to 13.1.2 (commit `3a3f15f`) — ensure any Aspire-specific instructions reflect current version
- AppHost UserSecretsId was updated (commit `67d0b74`) — may affect local configuration

### Latest Technical Information (as of 2026-02-27)

| Technology | Project Version | Latest Stable | Notes |
|-----------|----------------|---------------|-------|
| .NET 10 SDK | 10.0.102+ (`global.json`) | 10.0.103 (Feb 2026) | Security fixes in .103 — both work |
| DAPR CLI | 1.16.x+ | 1.16.9 (Feb 2026) | Compatible with SDK 1.16.1 |
| DAPR SDK | 1.16.1 (`Directory.Packages.props`) | 1.16.1 | Current |
| Aspire | 13.1.2 (`Directory.Packages.props`) | 13.1.2 | Recently updated in project |
| Docker Desktop | Latest stable | 4.x | No version pinning needed |

**Key notes for quickstart content:**
- .NET Aspire `dotnet run` or `dotnet aspire run` — verify which command the AppHost expects. With Aspire 13.x, the standard approach is `dotnet run --project src/Hexalith.EventStore.AppHost`
- The Aspire dashboard URL is typically printed to console on startup — document that the developer should look for it in terminal output rather than hard-coding a port
- DAPR sidecar injection happens automatically via Aspire — the developer does NOT need to run `dapr run` manually
- First run will take longer (NuGet restore + Docker image pulls) — warn the developer this is normal

### Anti-Patterns — What NOT to Do

| Anti-Pattern | Why It's Harmful |
|-------------|-----------------|
| Duplicating prerequisites content (tool installation, verification) | Already covered in `prerequisites.md` — just link to it |
| Hard-coding Aspire dashboard port (e.g., "open https://localhost:15888") | Port may vary; tell developer to read the terminal output |
| Hard-coding API port (e.g., "open http://localhost:8080/swagger") | Port may vary with config; guide developer to find it from Aspire dashboard |
| Inventing JSON payload shapes without reading source code | Payloads must match actual `CommandEnvelope` + `IncrementCounter` record types |
| Using `[!NOTE]` GitHub-flavored alerts | Not portable; use `> **Note:**` blockquote instead |
| Adding YAML frontmatter | GitHub renders it as visible text |
| Explaining DAPR architecture (sidecars, building blocks) | Violates progressive disclosure — quickstart is functional, not architectural |
| Explaining event sourcing theory (append-only, projections) | Not needed for quickstart; link to concepts pages for this |
| Writing "click here" link text | Poor accessibility; use descriptive link text |
| Using absolute URLs for internal docs links | Must use relative paths per D7 |
| Including error handling or edge cases | Quickstart shows happy path only |
| Using invented domain names (`MyCommand`, `SampleProcessor`) | Must use actual Counter domain names from the sample project |
| Telling users to run `dapr run` manually | DAPR sidecars are managed by Aspire — no manual `dapr run` needed |
| Assuming the reader has read the README or prerequisites | FR43 requires self-contained pages |

### NFRs This Story Supports

- **NFR1**: Quickstart completes in under 10 minutes on clean machine with prerequisites
- **NFR2**: Page renders on GitHub within 2 seconds, under 200KB
- **NFR6**: Heading hierarchy H1-H4 with no skipped levels
- **NFR9**: All code blocks with language-specific syntax highlighting tags
- **NFR10**: Maximum 2 prerequisite page dependencies (this page has 1: prerequisites.md)
- **NFR11**: Self-contained markdown — no cross-file build dependencies
- **NFR21**: Works identically on macOS, Windows, Linux with Docker Desktop
- **NFR25**: H1 title + one-paragraph summary optimized for SEO
- **NFR26**: Descriptive filename (`quickstart.md`)
- **NFR27**: 1-click depth from README (README → quickstart = direct link)

### FRs This Story Covers

- **FR7**: Developer can clone and run the sample with events flowing in under 10 minutes
- **FR10**: Developer can send a test command and observe the resulting event
- **FR42**: Cross-linking between related documentation pages via relative paths
- **FR43**: Page is self-contained — developer arriving from search can orient themselves

### Project Structure Notes

**File to modify:**
- `docs/getting-started/quickstart.md` — complete rewrite from stub

**Alignment with project structure:**
- `docs/getting-started/quickstart.md` matches D1 folder structure exactly
- Back-link `../../README.md` is correct relative path from `docs/getting-started/`
- Prerequisites link `prerequisites.md` is same-folder relative link
- Concepts links `../concepts/architecture-overview.md` and `../concepts/choose-the-right-tool.md` are correct relative paths

**Key source files the dev agent must read before writing content:**

```
src/Hexalith.EventStore.AppHost/
├── Program.cs                    ← Aspire topology, service definitions, ports
├── *.csproj                      ← Project references, Aspire version

src/Hexalith.EventStore.CommandApi/
├── Program.cs                    ← Endpoint definitions, Swagger setup
├── Controllers/ or Endpoints/    ← Command submission endpoint path + payload

src/Hexalith.EventStore.Contracts/
├── CommandEnvelope.cs (or similar) ← Wrapper type around commands

samples/Hexalith.EventStore.Sample/Counter/
├── Commands/IncrementCounter.cs  ← Command properties
├── Events/CounterIncremented.cs  ← Event properties
├── State/CounterState.cs         ← State properties
├── CounterProcessor.cs           ← Pure function implementation
```

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-2.1] — Story definition with BDD acceptance criteria
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#D1] — Content folder structure
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#D2] — Sample project architecture
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#D3] — CI pipeline and quickstart validation
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#D7] — Page template and cross-linking strategy
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR7] — 10-minute quickstart requirement
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR10] — Send command, observe event
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR42] — Cross-linking requirement
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR43] — Self-contained page requirement
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#Three-Act-Experience] — Write → Send → Watch flow
- [Source: docs/page-template.md] — Formatting conventions and page structure rules
- [Source: docs/getting-started/prerequisites.md] — Established prerequisite content (do not duplicate)
- [Source: _bmad-output/implementation-artifacts/8-3-prerequisites-and-local-dev-environment-page.md] — Previous story with conventions and patterns

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

No debug issues encountered. All source code files read successfully to extract actual API payloads, endpoints, and configuration.

### Completion Notes List

- Complete rewrite of `docs/getting-started/quickstart.md` from 12-line stub to full quickstart guide (120 lines, 5,564 bytes)
- All API payloads, endpoint paths, and run commands verified from actual source code (not invented):
  - `POST /api/v1/commands` from `CommandsController.cs` route attribute
  - `SubmitCommandRequest` fields from `Models/SubmitCommandRequest.cs`
  - `IncrementCounter` is parameterless (`sealed record IncrementCounter;`)
  - Domain name `counter` verified from `appsettings.Development.json` routing config
  - Keycloak test credentials from `hexalith-realm.json` realm import
  - Swagger UI at `/swagger` route prefix from `Program.cs` OpenAPI setup
- Added JWT authentication step (Get access token from Keycloak) since all API endpoints require `[Authorize]`
- Included cross-platform PowerShell alternative for curl token request (AC3)
- Page follows D7 template exactly: back-link, H1, summary, prerequisites, content, Next Steps
- DAPR explained at functional level only per progressive explanation pattern
- All internal links use relative paths (FR42)
- Page is self-contained for search arrivals (FR43)
- No YAML frontmatter, no emojis, no `[!NOTE]` alerts, all code blocks tagged

### File List

- `docs/getting-started/quickstart.md` — complete rewrite (modified)

### Change Log

- 2026-02-27: Complete rewrite of quickstart guide from stub to full 10-minute walkthrough covering clone, run, authenticate, send command, and observe events via Aspire dashboard
- 2026-02-27: Code review fixes applied — H1: removed contradictory Aspire dashboard instruction for Keycloak URL (port 8180 is the configured default, stated directly); H2: moved PowerShell alternative from inline blockquote to proper `powershell` code block (NFR9 compliance); M1: added explicit note to omit `Bearer ` prefix in Swagger authorize dialog; M2: made status endpoint actionable with endpoint pattern `/api/v1/commands/status/{correlationId}`; M3: added cross-platform note that `\` continuation requires bash/Zsh/PowerShell 7+
