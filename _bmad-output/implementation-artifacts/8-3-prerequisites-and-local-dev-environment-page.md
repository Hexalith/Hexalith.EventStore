# Story 8.3: Prerequisites & Local Dev Environment Page

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer ready to try Hexalith,
I want a clear prerequisites page listing everything I need installed,
so that I can set up my local environment before starting the quickstart.

## Acceptance Criteria

1. **AC1 - Page Template Compliance**: The page at `docs/getting-started/prerequisites.md` follows the standard page template: back-link to README (`[← Back to Hexalith.EventStore](../../README.md)`), H1 title, one-paragraph summary, content sections, Next Steps footer

2. **AC2 - Prerequisites Listed with Versions**: The page lists ALL required prerequisites with version numbers:
   - .NET 10 SDK (10.0.102 or later)
   - Docker Desktop (latest stable)
   - DAPR CLI (1.16.x or later)

3. **AC3 - Verification Commands**: Each prerequisite includes a verification command that the developer can run to confirm correct installation:
   - `$ dotnet --version` → expects 10.x output
   - `$ docker --version` → expects Docker version output
   - `$ dapr --version` → expects CLI and runtime version output

4. **AC4 - Installation Links**: Each prerequisite includes a link to the official installation page for Windows, macOS, and Linux

5. **AC5 - DAPR Initialization**: The page includes instructions for initializing DAPR after CLI installation (`dapr init`) with expected output verification

6. **AC6 - Quickstart Next Step**: The page links to the quickstart guide (`quickstart.md`) as the primary Next Step

7. **AC7 - Self-Contained (FR43)**: A developer arriving from a search engine understands the page without reading other pages first — the page explains WHY each tool is needed in context of Hexalith.EventStore

8. **AC8 - Relative Links Only**: All internal links use relative paths (no absolute URLs for internal documentation)

9. **AC9 - Heading Hierarchy (NFR6)**: Page uses H1 → H2 → H3 hierarchy with no skipped levels

10. **AC10 - Code Block Language Tags (NFR9)**: All code blocks specify language tags (`bash` for terminal commands)

11. **AC11 - No YAML Frontmatter**: Page does NOT use YAML frontmatter

12. **AC12 - Cross-Platform Coverage (NFR21)**: Installation instructions or links cover all three platforms: Windows, macOS, and Linux

## Tasks / Subtasks

- [x] Task 1: Create prerequisites.md page structure (AC: 1, 9, 11)
  - [x] Remove `.gitkeep` from `docs/getting-started/` if prerequisites.md replaces it as folder content marker
  - [x] Add back-link to README: `[← Back to Hexalith.EventStore](../../README.md)`
  - [x] Add H1 title: `# Prerequisites`
  - [x] Add summary paragraph explaining this page helps developers set up their environment before the quickstart
  - [x] Ensure heading hierarchy: H1 → H2 → H3, no skipped levels

- [x] Task 2: Add .NET 10 SDK section (AC: 2, 3, 4, 10, 12)
  - [x] Add H2 section: `## .NET 10 SDK`
  - [x] One-sentence context: why .NET 10 SDK is needed (Hexalith.EventStore targets .NET 10)
  - [x] Link to official .NET 10 download page: `https://dotnet.microsoft.com/en-us/download/dotnet/10.0`
  - [x] Add verification command in `bash` code block: `$ dotnet --version`
  - [x] Show expected output format (10.0.xxx)
  - [x] Note: any .NET 10 SDK 10.0.102 or later works (match project's `global.json` rollForward: latestPatch)

- [x] Task 3: Add Docker Desktop section (AC: 2, 3, 4, 10, 12)
  - [x] Add H2 section: `## Docker Desktop`
  - [x] One-sentence context: why Docker is needed (DAPR uses containers for local development infrastructure — state store, pub/sub, placement service)
  - [x] Link to official Docker Desktop install page: `https://docs.docker.com/desktop/`
  - [x] Note Windows-specific WSL 2 requirement
  - [x] Add verification command in `bash` code block: `$ docker --version`
  - [x] Add verification that Docker daemon is running: `$ docker info`

- [x] Task 4: Add DAPR CLI section (AC: 2, 3, 4, 5, 10, 12)
  - [x] Add H2 section: `## DAPR CLI`
  - [x] One-sentence DAPR context at functional level: "DAPR provides the infrastructure abstraction layer — it handles state storage, message delivery, and actor management so your domain logic stays pure"
  - [x] Link to official DAPR CLI install page: `https://docs.dapr.io/getting-started/install-dapr-cli/`
  - [x] Provide install commands for each platform in `bash` code blocks:
    - Windows (PowerShell): `powershell -Command "iwr -useb https://raw.githubusercontent.com/dapr/cli/master/install/install.ps1 | iex"`
    - macOS/Linux: `wget -q https://raw.githubusercontent.com/dapr/cli/master/install/install.sh -O - | /bin/bash`
  - [x] Add verification command: `$ dapr --version`
  - [x] Add DAPR initialization step: `$ dapr init`
  - [x] Show expected output (containers running: dapr_placement, dapr_redis, dapr_zipkin)
  - [x] Add verification that DAPR is initialized: `$ dapr --version` showing both CLI and runtime versions
  - [x] Note: DAPR CLI 1.16.x or later recommended (aligned with project's DAPR SDK 1.16.1)

- [x] Task 5: Add verification checklist section (AC: 3, 7)
  - [x] Add H2 section: `## Verify Your Environment`
  - [x] Provide a consolidated quick-check script or command sequence that verifies all prerequisites at once
  - [x] Show all-green expected output so developer knows they're ready

- [x] Task 6: Add troubleshooting tips section (AC: 7)
  - [x] Add H2 section: `## Common Issues`
  - [x] Cover most common setup problems:
    - Docker daemon not running
    - DAPR init fails (Docker not running)
    - .NET SDK version mismatch
    - Windows: WSL 2 not enabled
  - [x] Keep brief — link to official docs for detailed troubleshooting

- [x] Task 7: Add Next Steps footer (AC: 1, 6, 8)
  - [x] Add H2 section: `## Next Steps`
  - [x] Primary next: `[Quickstart Guide](quickstart.md)` — "Clone the repo and run the sample in under 10 minutes"
  - [x] Related: `[README](../../README.md)`, `[Choose the Right Tool](../concepts/choose-the-right-tool.md)`

- [x] Task 8: Final validation (AC: 1, 8, 9, 10, 11)
  - [x] Verify back-link to README uses correct relative path
  - [x] Verify heading hierarchy: H1 → H2 → H3, no skipped levels
  - [x] Verify all code blocks have language tags (`bash`)
  - [x] Verify no YAML frontmatter
  - [x] Verify all internal links use relative paths
  - [x] Verify no `[!NOTE]` alerts (use `> **Note:**` instead)
  - [x] Verify no hard-coded absolute URLs for internal links
  - [x] Verify page is self-contained — reads coherently without prior pages

## Dev Notes

### Architecture Source

This story implements **FR6** (prerequisites identification), **FR43** (self-contained page), and **FR53** (local dev environment setup) from `_bmad-output/planning-artifacts/prd-documentation.md`, following the page structure defined in **Decision D7** from `_bmad-output/planning-artifacts/architecture-documentation.md`.

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

**For the prerequisites page specifically:** This page has NO prerequisites callout (it IS the prerequisite page). Omit the prerequisites blockquote section entirely.

### Key Technical Decisions

**Exact Version Requirements (from project configuration):**

| Tool | Version | Source |
|------|---------|--------|
| .NET SDK | 10.0.102 or later (rollForward: latestPatch) | `global.json` |
| Target Framework | net10.0 | `Directory.Build.props` |
| DAPR SDK packages | 1.16.1 | `Directory.Packages.props` |
| DAPR CLI | 1.16.x or later (aligned with SDK) | DAPR compatibility matrix |
| CommunityToolkit.Aspire.Hosting.Dapr | 13.0.0 | `Directory.Packages.props` |
| Docker Desktop | Latest stable | DAPR requirement |

**DAPR Explanation Depth for Prerequisites Page:**

Per the progressive explanation pattern in the architecture document:

| Page Type | DAPR Explanation Depth |
|-----------|----------------------|
| README | One sentence: "Built on DAPR for infrastructure portability" |
| **Prerequisites (this page)** | **Installation-focused: What DAPR is (one sentence), how to install it, how to verify it works. No architectural details.** |
| Quickstart | Functional: "DAPR handles message delivery and state storage — you don't write infrastructure code" |
| Concepts pages | Architectural: which DAPR building blocks are used and why |

Keep DAPR explanation to ONE functional sentence, then focus entirely on installation and verification. Example: "DAPR provides the infrastructure layer that handles state storage, messaging, and actor management — your domain code never touches infrastructure directly."

**DAPR Init Expected Containers:**

After running `dapr init`, the developer should see these Docker containers:
- `dapr_placement` — actor placement service
- `dapr_redis` — default state store and pub/sub for local development
- `dapr_zipkin` — distributed tracing

Verification: `docker ps` should show all three containers running.

**Cross-Platform Installation Notes:**

| Platform | .NET SDK | Docker | DAPR CLI |
|----------|----------|--------|----------|
| Windows | MSI or `winget install Microsoft.DotNet.SDK.10` | Docker Desktop (requires WSL 2) | PowerShell script or `winget install Dapr.CLI` |
| macOS | `.pkg` installer or `brew install dotnet-sdk` | Docker Desktop | `brew install dapr/tap/dapr-cli` or curl script |
| Linux | Package manager (apt, dnf) or install script | Docker Desktop or Docker Engine | curl/wget install script |

**Do NOT reproduce full installation instructions** — link to official docs. The prerequisites page should be a checklist with verification commands, not a substitute for official installation guides.

### Content Voice and Tone

- **Second person**: "you", "your" — "Before you start the quickstart..."
- **Professional-casual**: Developer-to-developer, not marketing or academic
- **Active voice**: "Install the .NET 10 SDK" not "The .NET 10 SDK should be installed"
- **Assume reader knows .NET** but NOT DAPR or Aspire
- **Callouts**: Use `> **Note:**` or `> **Tip:**` — NEVER use `[!NOTE]` GitHub alerts
- **No emojis** in the documentation page itself

### NFRs This Story Supports

- **NFR6**: Heading hierarchy H1-H4 with no skipped levels
- **NFR9**: All code blocks with language-specific syntax highlighting tags
- **NFR10**: Maximum 2 prerequisite page dependency (this page has zero prerequisites)
- **NFR11**: Self-contained — page works for readers arriving from search
- **NFR21**: Cross-platform (Windows, macOS, Linux) coverage
- **NFR25**: H1 title + one-paragraph summary
- **NFR26**: Descriptive filename (`prerequisites.md`)
- **NFR27**: 2-click depth from README (README → prerequisites = 1 click)

### FRs This Story Covers

- **FR6**: Developer can identify all prerequisites needed before quickstart
- **FR43**: Page is self-contained — developer arriving from search understands without reading other pages
- **FR53**: Developer can set up local development environment matching documented configuration

### Cross-Linking Requirements (D7)

Prerequisites page links to:
- `../../README.md` — back-link at top of page
- `quickstart.md` — Next Step (same folder, relative link)
- `../concepts/choose-the-right-tool.md` — Related link (for developers still evaluating)
- External links: .NET download, Docker Desktop install, DAPR CLI install (official docs only)

Prerequisites page is linked FROM:
- `README.md` — "Prerequisites" in the Getting Started section
- `docs/getting-started/quickstart.md` — (future) prerequisites callout at top

### Anti-Patterns — What NOT to Do

| Anti-Pattern | Why It's Harmful |
|-------------|-----------------|
| Reproducing full installation instructions for each tool | Goes stale; link to official docs instead |
| Hard-coding specific patch versions (e.g., "install 10.0.103") | Goes stale; use minimum version + "or later" |
| Using `[!NOTE]` GitHub-flavored alerts | Not portable; use `> **Note:**` blockquote instead |
| Adding YAML frontmatter | GitHub renders it as visible text |
| Explaining DAPR architecture on this page | Violates progressive disclosure; this is installation, not understanding |
| Writing "click here" link text | Poor accessibility; use descriptive link text |
| Assuming the reader has read the README first | FR43 requires self-contained pages |
| Providing Docker Compose files or DAPR component configs | That belongs in quickstart/deployment stories |
| Using absolute URLs for internal docs links | Must use relative paths per D7 |

### Project Structure Notes

**File to create:**
- `docs/getting-started/prerequisites.md` — new file (currently `.gitkeep` placeholder)

**File to potentially remove:**
- `docs/getting-started/.gitkeep` — only if other files now exist in the folder (check if `.gitkeep` coexists with real files in the project convention; Story 8-1 placed `.gitkeep` as placeholder)

**Files to reference (read-only):**
- `docs/page-template.md` — formatting conventions and page structure
- `global.json` — .NET SDK version requirement (10.0.102, rollForward: latestPatch)
- `Directory.Build.props` — target framework (net10.0)
- `Directory.Packages.props` — DAPR SDK version (1.16.1), Aspire version (13.1.1)
- `README.md` — verify prerequisites link points correctly to new page

**Alignment with project structure:**
- `docs/getting-started/prerequisites.md` matches D1 folder structure exactly
- Back-link `../../README.md` is correct relative path from `docs/getting-started/`
- Quickstart link `quickstart.md` is same-folder relative link

### Previous Story Intelligence (8-2)

**Story 8-2 (README Rewrite with Progressive Disclosure) — status: review:**
- README.md now includes a "Get Started" section linking to `docs/getting-started/quickstart.md`
- README mentions "Prerequisites: .NET SDK, Docker Desktop, DAPR CLI" inline
- README uses all established conventions (heading hierarchy, code block tags, relative links, no frontmatter)
- The README links to `docs/getting-started/prerequisites.md` is expected in the documentation navigation section
- Page template conventions from Story 8-1 are fully established and documented in `docs/page-template.md`

**What this means for Story 8-3:**
- The prerequisites page is already linked from the README — it needs to exist and work
- Follow the exact same conventions demonstrated in 8-2: second person voice, professional-casual tone
- The page should complement the README's brief mention of prerequisites with full detail
- Developers arriving from the README "Get Started" section expect to find actionable setup instructions

### Git Intelligence

Recent commits show documentation initiative progress:
- `2d4f3fb` — Merge PR #60: Story 8-1 implementation and Story 8-2 artifact
- `a34fbc6` — Story 8.1 implementation (folder structure) and Story 8.2 artifact creation
- `207c6d3` — settings.json for permission configuration
- `f7f1d35` — Merge PR #59: Story 7.8 fixes and Epic 8 init
- `ec6bf5a` — Story 7.8 code review fixes and Story 8.1 artifact

**Patterns observed:**
- Commit messages use conventional format: `chore:`, `docs:`, `fix:`
- PRs use branch naming: `chore/story-X-Y-description`
- Stories are implemented and reviewed in sequence

### Latest Technical Information

**Verified versions as of 2026-02-26:**

| Tool | Latest Stable | Project Minimum | Install Link |
|------|--------------|-----------------|--------------|
| .NET 10 SDK | 10.0.103 (Feb 10, 2026) | 10.0.102 | https://dotnet.microsoft.com/en-us/download/dotnet/10.0 |
| DAPR CLI | 1.16.9 (Feb 12, 2026) | 1.16.x | https://docs.dapr.io/getting-started/install-dapr-cli/ |
| Docker Desktop | Latest stable | Latest stable | https://docs.docker.com/desktop/ |

**Key notes for developer:**
- .NET 10 SDK 10.0.103 includes security fixes from Feb 2026 servicing update
- DAPR CLI 1.16.9 is compatible with DAPR SDK 1.16.1 used by the project
- Docker Desktop on Windows requires WSL 2 (version 2.1.5 or later)
- `dapr init` requires Docker to be running — document this dependency order

### Testing Standards

This story produces a single markdown file (`docs/getting-started/prerequisites.md`). Validation:

1. **Page template compliance**: Verify back-link, H1, summary, content sections, Next Steps footer
2. **Heading hierarchy check**: Verify H1 → H2 → H3 with no skipped levels
3. **Code block language check**: Verify every code fence has a `bash` language tag
4. **No frontmatter**: Verify no YAML frontmatter at top of file
5. **Link check**: Verify all internal links use relative paths and point to existing files/placeholders
6. **Self-contained check**: Read the page in isolation — does it make sense without prior context?
7. **Verification commands**: Manually run each verification command to confirm they produce the documented output
8. **Cross-platform**: Verify installation links cover Windows, macOS, and Linux
9. **No `[!NOTE]` alerts**: Verify only `> **Note:**` blockquote syntax is used

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-1.3] — Story definition with BDD acceptance criteria
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#D7] — Page template and cross-linking strategy
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#D1] — Content folder structure
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR6] — Prerequisites identification requirement
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR43] — Self-contained page requirement
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR53] — Local dev environment setup requirement
- [Source: docs/page-template.md] — Formatting conventions and page structure rules
- [Source: _bmad-output/implementation-artifacts/8-2-readme-rewrite-with-progressive-disclosure.md] — Previous story output and conventions
- [Source: global.json] — .NET SDK version requirement (10.0.102)
- [Source: Directory.Build.props] — Target framework (net10.0)
- [Source: Directory.Packages.props] — DAPR SDK version (1.16.1), Aspire version (13.1.1)

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

No issues encountered. All tasks completed in a single pass.

### Completion Notes List

- Complete rewrite of `docs/getting-started/prerequisites.md` from placeholder to full prerequisites page
- Page follows standard page template: back-link, H1, summary paragraph, content sections, Next Steps footer
- .NET 10 SDK section: version 10.0.102+ requirement (from global.json), download link, verification command
- Docker Desktop section: context for why Docker is needed (DAPR containers), download link, WSL 2 note for Windows, verification commands (docker --version, docker info)
- DAPR CLI section: functional DAPR explanation, install commands for Windows (PowerShell + winget), macOS (brew), and Linux (wget), verification command, dapr init instructions with expected containers (dapr_placement, dapr_redis, dapr_zipkin), runtime verification
- Verify Your Environment section: consolidated 4-command quick-check sequence
- Common Issues section: 4 troubleshooting tips (Docker not running, DAPR init fails, .NET version mismatch, WSL 2 not enabled) with links to official docs
- Next Steps footer: primary link to quickstart.md, related links to README and choose-the-right-tool
- All 13 code blocks use `bash` language tag with `$` command prefix
- Heading hierarchy H1 → H2 → H3 with no skipped levels
- All internal links use relative paths, all link targets verified to exist
- No YAML frontmatter, no [!NOTE] alerts
- Page is self-contained per FR43 — explains WHY each tool is needed in Hexalith.EventStore context
- Cross-platform coverage for Windows, macOS, and Linux per NFR21
- .gitkeep already absent from docs/getting-started/ (removed during 8-2 file operations)

### Change Log

- 2026-02-26: Complete prerequisites.md page implementing all 12 acceptance criteria across 8 tasks
- 2026-02-26: Senior Developer Review (AI) completed; corrected cross-platform installation link granularity and verification robustness in prerequisites page

### File List

- docs/getting-started/prerequisites.md (modified — complete rewrite from placeholder to full prerequisites page)
- _bmad-output/implementation-artifacts/8-3-prerequisites-and-local-dev-environment-page.md (modified — story tracking file)
- _bmad-output/implementation-artifacts/sprint-status.yaml (modified — story status updates)

## Senior Developer Review (AI)

### Reviewer

GPT-5.3-Codex

### Review Date

2026-02-26

### Outcome

Approve

### Story vs Git Validation

- Story File List matched git-modified files exactly:
  - `docs/getting-started/prerequisites.md`
  - `_bmad-output/implementation-artifacts/8-3-prerequisites-and-local-dev-environment-page.md`
  - `_bmad-output/implementation-artifacts/sprint-status.yaml`
- No undocumented source-code file changes detected.

### Findings

#### HIGH

1. AC4/AC12 ambiguity risk: prerequisite sections did not consistently provide explicit platform-specific official installation links for Windows, macOS, and Linux.
  - **Fix applied:** Added platform-specific official links for .NET, Docker, and DAPR CLI.

#### MEDIUM

2. Docker verification expectation was brittle (`Docker version 27.x.x or later`) and could age quickly.
  - **Fix applied:** Replaced with stable expectation (`output starts with Docker version`).

3. Consolidated environment check omitted `docker info`, reducing confidence that daemon readiness is validated in the single quick-check sequence.
  - **Fix applied:** Added `docker info` to the consolidated verification commands and updated expected command count.

#### LOW

4. No additional low-severity issues found after fixes.

### Acceptance Criteria Re-Validation (Post-Fix)

- AC1 ✅ Page template structure present (back-link, H1, summary, content sections, Next Steps)
- AC2 ✅ Required prerequisites and versions present
- AC3 ✅ Verification commands included
- AC4 ✅ Official installation links include Windows/macOS/Linux coverage
- AC5 ✅ `dapr init` instructions and verification present
- AC6 ✅ Next step links to `quickstart.md`
- AC7 ✅ Self-contained rationale for each prerequisite
- AC8 ✅ Internal links are relative
- AC9 ✅ Heading hierarchy H1 → H2 → H3
- AC10 ✅ Code blocks are language-tagged (`bash`)
- AC11 ✅ No YAML frontmatter
- AC12 ✅ Cross-platform coverage explicit

### Review Follow-ups (AI)

- [x] [AI-Review][HIGH] Add explicit platform-specific official installation links for .NET, Docker, and DAPR CLI
- [x] [AI-Review][MEDIUM] Remove brittle Docker version expectation
- [x] [AI-Review][MEDIUM] Add `docker info` to consolidated environment verification
