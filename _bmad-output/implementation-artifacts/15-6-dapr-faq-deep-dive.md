# Story 15.6: DAPR FAQ Deep Dive

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer with concerns about the DAPR dependency,
I want a comprehensive FAQ addressing DAPR-specific questions and risks,
so that I can make an informed decision about adopting Hexalith.

## Acceptance Criteria

1. **Given** a developer navigates to `docs/guides/dapr-faq.md` **When** they read the page **Then** the page provides deep, honest answers to: What if DAPR is deprecated? How does DAPR versioning affect Hexalith? What's the performance overhead of DAPR sidecars? Can I use Hexalith without DAPR? What are the operational costs of running DAPR?
2. **And** DAPR is explained at deep depth per the progressive explanation pattern — honest trade-off analysis, risk assessment, what-if scenarios
3. **And** the page follows the standard page template: back-link `[← Back to Hexalith.EventStore](../../README.md)`, H1, summary paragraph, prerequisites/tip callout, content sections, Next Steps footer
4. **And** the page is self-contained (FR43) — makes sense to someone landing on it directly, not only via README navigation
5. **And** existing "future DAPR FAQ Deep Dive" references in `docs/concepts/choose-the-right-tool.md` are updated to remove the "future" qualifier
6. **And** the `.lycheeignore` and `lychee.toml` dead-link exclusions for `dapr-faq` are removed
7. **And** `README.md` Guides section includes a link to the DAPR FAQ
8. **And** markdownlint-cli2 passes with project config (`.markdownlint-cli2.jsonc`)

## Tasks / Subtasks

- [x] Task 1: Create `docs/guides/dapr-faq.md` (AC: #1, #2, #3, #4)
    - [x] 1.1 Write page following standard template: back-link `[← Back to Hexalith.EventStore](../../README.md)`, H1 "DAPR FAQ Deep Dive", summary paragraph explaining this is the deepest level of DAPR coverage. The summary must work for a cold Google landing — include one sentence: "Hexalith.EventStore is a DAPR-native event sourcing server for .NET — this page answers the hard questions about what that DAPR dependency means for you."
    - [x] 1.2 Add prerequisites blockquote linking to [Choose the Right Tool](../concepts/choose-the-right-tool.md) and [Architecture Overview](../concepts/architecture-overview.md)
    - [x] 1.3 Section: "What Is DAPR and Why Does Hexalith Use It?" — brief recap (1 paragraph) with link to architecture-overview for full explanation. Do NOT repeat what's already in choose-the-right-tool — reference and deepen
    - [x] 1.4 Section: "What if DAPR Is Deprecated?" — deep analysis. Cover: CNCF graduated status (Feb 2024), governance independence from Microsoft, what graduation means, Hexalith's architectural isolation (runtime replacement concentrated in the Server package while domain code remains DAPR-free), domain code portability (Handle/Apply pure functions have zero DAPR imports), migration effort estimate (Server package replacement scope), comparison with similar CNCF project lifecycle. Acknowledge the competitive context: alternatives like Marten and EventStoreDB do not carry this risk — be upfront about that, then explain why the trade-off is worth it for the right use case
    - [x] 1.5 Section: "How Does DAPR Versioning Affect Hexalith?" — deep analysis. Cover: DAPR SemVer policy, v1.x stability since Feb 2021 (5+ years), current pinned version (1.16.1 in Directory.Packages.props), Hexalith CI verification, upgrade strategy (bump in feature branch, run test suite), major version upgrade cadence and migration guidance. Note that DAPR continues adding new building blocks (e.g., Workflow in v1.14) but Hexalith currently uses State Store, Pub/Sub, Actors, Service Invocation, and Configuration Store — new DAPR features do not affect Hexalith unless explicitly adopted. Include a "Last verified" date for all DAPR version claims so readers know when the content was last reviewed
    - [x] 1.6 Section: "What Is the Performance Overhead of DAPR Sidecars?" — deep analysis with quantitative context. Cover: localhost gRPC hop (microseconds-to-low-milliseconds), no network hop (same-host communication), batch operation amortization, comparison with direct DB access, sidecar resource consumption (CPU/memory baseline), when this overhead matters vs when it doesn't, reference to DAPR's own benchmark documentation
    - [x] 1.7 Section: "Can I Use Hexalith Without DAPR?" — honest answer. Cover: no — DAPR is a core runtime dependency, NOT optional. Explain the architectural reasoning (Hexalith would need to rebuild state store abstraction, pub/sub, actors, service discovery). Explain the isolation guarantee (domain code is DAPR-free even though some supporting packages include DAPR-facing integration helpers). Explain what "without DAPR" would mean practically (replace the Server package first, then update the DAPR-facing seams in hosting/integration helpers). Acknowledge the competitive context: if DAPR is a dealbreaker, Marten (in-process, PostgreSQL only) and EventStoreDB (dedicated server, no sidecar) are viable alternatives with no DAPR dependency — link to choose-the-right-tool for the full comparison
    - [x] 1.8 Section: "What Are the Operational Costs of Running DAPR?" — deep analysis. Cover: sidecar per-instance overhead (CPU, memory), DAPR placement service (required for actors), component YAML management, version coordination across services, monitoring/debugging (DAPR dashboard, OpenTelemetry integration), documented deployment path (Docker/K8s/ACA) plus local slim-mode caveats, comparison with alternatives (in-process library like Marten = zero, dedicated server like EventStoreDB = server cluster). Reference DAPR's production deployment guidance for sidecar resource limits and include pod sizing implications for Kubernetes deployments
    - [x] 1.9 Section: "What Are the Backend-Specific Consistency Differences?" — cover the caveat mentioned in choose-the-right-tool. Not all DAPR state store backends support identical consistency guarantees. Infrastructure portability means portable code, not portable behavior. Focus on the three backends used in Hexalith's sample and deployment guides: Redis, PostgreSQL, and Cosmos DB — mention that DAPR supports other backends but these three cover the most common deployment scenarios. Do not attempt to document consistency for all DAPR state stores. Reference DAPR component specification pages for backend-specific consistency guarantees — do not state guarantees without a verifiable source
    - [x] 1.10 Section: "What If a Better Abstraction Emerges?" — deeper expansion of what's in choose-the-right-tool. Contracts and domain code are DAPR-free; the Server package contains most DAPR integration (actor lifecycle, event persistence, snapshot, pub/sub, idempotency), while smaller DAPR-facing seams also exist in hosting/integration helpers. Replacing Server is significant but scoped. Domain code survives any runtime change
    - [x] 1.11 Section: "How Does Hexalith Handle DAPR Sidecar Failures?" — cover: sidecar health checks, retry policies, circuit breakers, what happens when sidecar crashes mid-operation, Aspire auto-restart in development, Kubernetes liveness/readiness probes in production, dead-letter routing for failed event delivery. Specifically cover the persist-then-publish atomicity gap: what happens when the sidecar fails after persisting events but before publishing them? Answer: events are already persisted safely, pub/sub delivery retries on sidecar recovery, and consumer idempotency (causation ID check) handles any duplicate deliveries. Include a subsection on debugging the DAPR sidecar chain: where to find sidecar logs, how errors propagate (app → sidecar → backend → sidecar → app), DAPR dashboard for component-level visibility, and the OpenTelemetry trace correlation that Hexalith provides. Link to troubleshooting.md for specific error resolutions
    - [x] 1.12 Next Steps footer: links to [Choose the Right Tool](../concepts/choose-the-right-tool.md), [Architecture Overview](../concepts/architecture-overview.md), [DAPR Component Configuration Reference](dapr-component-reference.md), [Troubleshooting Guide](troubleshooting.md)
- [x] Task 2: Update cross-references in `docs/concepts/choose-the-right-tool.md` (AC: #5)
    - [x] 2.1 Find all instances of "future [DAPR FAQ Deep Dive]" or "future DAPR FAQ Deep Dive" and remove the word "future"
    - [x] 2.2 Verify all `../guides/dapr-faq.md` links are correct (they already exist, just need "future" removed from surrounding text)
    - [x] 2.3 Update the note at line ~219: remove "future" qualifier from the sentence
    - [x] 2.4 Verify the DAPR SDK version mentioned in choose-the-right-tool.md (currently says 1.16.1 at line ~193) matches the actual version in `Directory.Packages.props` — update if stale
- [x] Task 3: Update link exclusion files (AC: #6)
    - [x] 3.1 In `.lycheeignore`: remove lines 26-27 (`^\.\./guides/dapr-faq\.md$` and `^docs/guides/dapr-faq\.md$`)
    - [x] 3.2 In `lychee.toml`: remove `'guides/dapr-faq'` from the exclusion list (line ~72)
- [x] Task 4: Update README.md (AC: #7)
    - [x] 4.1 Add `[DAPR FAQ Deep Dive](docs/guides/dapr-faq.md) — honest answers about DAPR dependency, risks, and operational costs` to the Guides section
- [x] Task 5: Validate documentation changes (AC: #8)
    - [x] 5.1 Run `npx markdownlint-cli2 docs/guides/dapr-faq.md` — 0 errors
    - [x] 5.2 Run `npx markdownlint-cli2 docs/concepts/choose-the-right-tool.md` — 0 errors (after edits)
    - [x] 5.3 Verify all internal links resolve to existing files
        - [ ] 5.4 Run `lychee --config lychee.toml docs/` to verify zero broken links across the entire docs folder after all edits (not just the new file) — blocked by pre-existing unrelated failures in `docs/reference/api/`, `docs/community/`, and `docs/guides/troubleshooting.md`
        - [x] 5.5 Run `lychee --config lychee.toml docs/guides/dapr-faq.md docs/concepts/choose-the-right-tool.md README.md` after review fixes — 0 story-scoped errors

## Dev Notes

### Architecture Context: Progressive DAPR Explanation Pattern

This story creates the **deepest level** of the progressive DAPR explanation pattern established across the documentation:

| Level | Page                                | DAPR Depth                                                |
| ----- | ----------------------------------- | --------------------------------------------------------- |
| 1     | README.md                           | One sentence                                              |
| 2     | Quickstart                          | Functional (just works)                                   |
| 3     | Architecture Overview               | Architectural (topology, building blocks)                 |
| 4     | Choose the Right Tool               | Architectural + trade-offs                                |
| 5     | **DAPR FAQ Deep Dive (this story)** | **Deep — risk assessment, benchmarks, what-if scenarios** |

The FAQ page is NOT a repeat of choose-the-right-tool — it goes deeper on risk, operations, and migration scenarios. Reference choose-the-right-tool for the intro, then deepen. Use a question-answer format that feels like a real FAQ a developer would ask before adopting.

### Content Strategy: Honest Developer-to-Developer Voice

The DAPR FAQ page must be **brutally honest**. Developers evaluating Hexalith will come to this page specifically to find gotchas and risks. If the page reads like marketing, it fails. Follow the pattern established in choose-the-right-tool.md: when Hexalith is NOT the right choice, say so explicitly. When DAPR has genuine downsides, name them. Trust is built through honesty.

Tone: second person voice, professional-casual, developer-to-developer perspective. Same style as all existing doc pages.

### FAQ Section Structure

Each FAQ section MUST follow this pattern for scannability:

1. **Bold question as H2** (e.g., `## What if DAPR Is Deprecated?`)
2. **TL;DR answer in a blockquote** — 1-2 sentences, the direct answer. Developers skim; give them the answer first
3. **Deep analysis** — the full explanation with evidence, trade-offs, and nuance
4. **Risk-payoff pairing** — every honest admission of a DAPR risk must close with the value proposition of accepting that risk (e.g., "Yes, DAPR is a hard dependency — and that dependency is what gives you infrastructure portability across every cloud provider")
5. **See also** — 1-liner at the end pointing to 1-2 related sections on this page or other docs (e.g., "For how Hexalith isolates your domain code from DAPR, see the 'What If DAPR Is Deprecated?' section above")

### Content Duplication Anti-Pattern

**ANTI-PATTERN:** If any paragraph in `dapr-faq.md` could be copy-pasted from `choose-the-right-tool.md`, the implementation has failed. Every section must reference the existing page for background and then provide NEW content not found elsewhere. Pattern: _"For background on [topic], see [Choose the Right Tool](...). Here, we go deeper:"_ followed by operational detail, quantitative context, or migration scenarios that choose-the-right-tool does not cover.

### Honest Voice: Good vs Bad Examples

Developers come to this page to find gotchas. If the page hedges or sounds like marketing, it fails.

**BAD:** _"While DAPR is currently required, Hexalith's architecture minimizes the impact and provides a smooth path forward..."_

**GOOD:** _"No. DAPR is a hard runtime dependency. Without it, Hexalith does not function. Here's why we made that choice and what it costs you:"_

### Quantitative Claims Policy

Every quantitative claim MUST include a citation or be explicitly marked as an estimate. Use phrasing like _"DAPR's localhost gRPC hop typically adds microseconds to low single-digit milliseconds (see [DAPR performance docs](...))"_ — never state a specific number without a verifiable source.

### Risk Assessment Summary Table

Consider including a summary risk assessment table at the top or bottom of the page:

| Risk            | Likelihood | Impact | Mitigation                                      | Residual Risk                      |
| --------------- | ---------- | ------ | ----------------------------------------------- | ---------------------------------- |
| DAPR deprecated | Low        | High   | Architectural isolation; Server-only dependency | Medium (Server replacement effort) |
| ...             | ...        | ...    | ...                                             | ...                                |

This serves the evaluating architect who needs a scannable risk profile for their architecture decision record.

### Target Page Length

The existing choose-the-right-tool page is ~4,500 words. For the deepest FAQ covering 9 sections, target **3,000–5,000 words**. Under 2,000 is too shallow for "deep" depth. Over 6,000 risks bloat — split into collapsible sections if needed.

### Quantitative Context for Performance and Operations Sections

Do NOT invent benchmark numbers or resource figures. Instead:

- Reference DAPR's own published performance data if available
- Describe the localhost gRPC hop in qualitative terms with order-of-magnitude context (microseconds to low milliseconds)
- Be clear about what has been measured vs what is estimated
- Link to DAPR docs for their performance testing methodology
- Mention that Hexalith uses gRPC (not HTTP) for sidecar communication, which is the faster protocol
- For sidecar resource consumption, reference DAPR's production deployment docs for recommended CPU/memory limits
- For pod sizing, reference DAPR's Kubernetes production configuration guidance

### Key File Locations

| File                                      | Purpose                                                    |
| ----------------------------------------- | ---------------------------------------------------------- |
| `docs/guides/dapr-faq.md`                 | **NEW** — target file                                      |
| `docs/concepts/choose-the-right-tool.md`  | Update: remove "future" qualifier from DAPR FAQ references |
| `docs/concepts/architecture-overview.md`  | Reference only — link target for prerequisites             |
| `docs/guides/dapr-component-reference.md` | Reference only — link target for Next Steps                |
| `docs/guides/troubleshooting.md`          | Reference only — link target for Next Steps                |
| `.lycheeignore`                           | Update: remove dapr-faq exclusion lines (26-27)            |
| `lychee.toml`                             | Update: remove dapr-faq from exclusion list (~line 72)     |
| `README.md`                               | Update: add DAPR FAQ link to Guides section                |
| `.markdownlint-cli2.jsonc`                | Linting config to validate against                         |
| `Directory.Packages.props`                | Reference: DAPR SDK version (currently 1.16.1)             |

### DAPR SDK Version

The current pinned DAPR SDK version is **1.16.1** (check `Directory.Packages.props` at implementation time for any updates). References to the DAPR version in the FAQ page should match whatever is in `Directory.Packages.props` when the story is implemented.

### Existing DAPR Content to Reference (Not Repeat)

The following DAPR content already exists in the docs. The FAQ page should **reference and deepen**, not repeat:

1. **choose-the-right-tool.md** — "The DAPR Trade-Off" section covers: why DAPR, what trade-offs (runtime dependency, sidecar latency, learning curve, version coupling), what if DAPR changes direction, Hexalith isolation guarantee. The FAQ deepens ALL of these with operational detail and quantitative context.

2. **architecture-overview.md** — "What Is DAPR?" section, DAPR Building Blocks (State Store, Pub/Sub, Service Invocation, Actors, Configuration Store), topology diagram. The FAQ can reference this for "how does DAPR work" questions.

3. **dapr-component-reference.md** — Comprehensive DAPR component YAML configurations for all backends. The FAQ should link here for "how do I configure DAPR?" questions.

4. **troubleshooting.md** — DAPR Integration Issues section covers: sidecar injection failure, state store connection timeout, pub/sub message loss, actor activation conflict, component configuration mismatch. The FAQ should link here for "what happens when things break?" questions.

### Cross-Reference Updates (choose-the-right-tool.md)

The following phrases need "future" removed:

1. Line ~185: `The future [DAPR FAQ Deep Dive](../guides/dapr-faq.md) will cover quantitative benchmarks.` → `The [DAPR FAQ Deep Dive](../guides/dapr-faq.md) covers quantitative benchmarks.`
2. Line ~207: `The deepest risk assessment — including DAPR performance benchmarks, operational cost analysis, and detailed migration scenarios — will be covered in a future [DAPR FAQ Deep Dive](../guides/dapr-faq.md).` → Remove "future" and change "will be covered in" to "is covered in"
3. Line ~215: `The future [DAPR FAQ Deep Dive](../guides/dapr-faq.md) will cover backend-specific consistency differences.` → `The [DAPR FAQ Deep Dive](../guides/dapr-faq.md) covers backend-specific consistency differences.`
4. Line ~219: `The deepest risk assessment... will be covered in a future [DAPR FAQ Deep Dive]...` — update to present tense

**IMPORTANT:** The dev agent MUST read the actual file at implementation time to find the exact line numbers and text — the lines above are approximate from the current version. The same applies to `.lycheeignore` and `lychee.toml` — grep for `dapr-faq` to find the exact lines rather than assuming line numbers, as they may have shifted.

### Lychee/Lint Exclusion Cleanup

Once `docs/guides/dapr-faq.md` exists, the dead-link exclusions must be removed:

**`.lycheeignore` lines to remove:**

```text
^\.\./guides/dapr-faq\.md$
^docs/guides/dapr-faq\.md$
```

**`lychee.toml` exclusion to remove:**

```text
'guides/dapr-faq',
```

### README.md Guides Section Update

Current Guides section:

```markdown
### Guides

- [Upgrade Path](docs/guides/upgrade-path.md) — migrating between versions
- [Deployment Guides](docs/guides/) — Docker Compose, Kubernetes, Azure Container Apps
```

Add:

```markdown
- [DAPR FAQ Deep Dive](docs/guides/dapr-faq.md) — honest answers about DAPR dependency, risks, and operational costs
```

### Project Structure Notes

- Target file: `docs/guides/dapr-faq.md` (new file)
- `docs/guides/` folder exists with multiple guide files — follow same conventions
- Back-link uses `../../README.md` (two levels up from `docs/guides/`)
- Code examples use Counter domain names (IncrementCounter, CounterProcessor, CounterState) — never invent new domain names
- No Mermaid diagrams expected for this page (FAQ format doesn't typically need diagrams), but include one if it genuinely aids understanding

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 8.6 — DAPR FAQ Deep Dive]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md — DAPR progressive explanation pattern]
- [Source: docs/concepts/choose-the-right-tool.md#The DAPR Trade-Off — existing DAPR trade-off analysis]
- [Source: docs/concepts/architecture-overview.md — DAPR building blocks and topology]
- [Source: docs/guides/dapr-component-reference.md — DAPR component configurations]
- [Source: docs/guides/troubleshooting.md — DAPR integration issues]
- [Source: _bmad-output/implementation-artifacts/sprint-status.yaml — story 15-6-dapr-faq-deep-dive: backlog]
- [Source: _bmad-output/implementation-artifacts/15-5-public-product-roadmap.md — previous story learnings]

### Previous Story Intelligence (from Story 15-5)

- **Page template:** back-link `[← Back to Hexalith.EventStore](../../README.md)`, H1, intro paragraph, prerequisites blockquote, content sections, Next Steps footer
- **markdownlint-cli2** must pass with project config (`.markdownlint-cli2.jsonc`)
- **Branch pattern:** `docs/story-15-6-dapr-faq-deep-dive`
- **Commit pattern:** `feat(docs): Add DAPR FAQ deep dive (Story 15-6)`
- **Internal links:** All internal links must resolve to existing files
- **Cross-reference updates** are part of the story (update choose-the-right-tool.md, .lycheeignore, lychee.toml, README.md)
- All doc stories: feature branch per story, single commit with `feat(docs):` prefix, merge via PR

### Git Intelligence

Recent commits show consistent documentation pattern:

```text
1d4eb5f docs: complete Story 15 documentation suite (#95)
b9e7897 fix: use VersionOverride for FluentUI packages in design directions prototype (#94)
a201d73 Merge pull request #93 from Hexalith/fix/sln-to-slnx-references
f825bf9 fix: replace .sln references with .slnx across docs
cf5d0bf Merge pull request #92 from Hexalith/docs/story-15-2-ready-for-dev
```

All doc stories follow: feature branch → single commit with `feat(docs):` prefix → merge via PR.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- `npx markdownlint-cli2 docs/guides/dapr-faq.md` — passes (0 errors)
- `npx markdownlint-cli2 docs/concepts/choose-the-right-tool.md` — passes (0 errors)
- `lychee --config lychee.toml docs/guides/dapr-faq.md docs/concepts/choose-the-right-tool.md README.md` — passes after review fixes (0 errors)
- `lychee --config lychee.toml docs/` — still reports pre-existing unrelated failures in `docs/reference/api/`, `docs/community/`, and one stale external Dapr docs URL in `docs/guides/troubleshooting.md`; not caused by Story 15-6

### Completion Notes List

- Created `docs/guides/dapr-faq.md` — comprehensive FAQ with 9 deep-dive sections, risk assessment summary table, TL;DR blockquotes per section, honest developer-to-developer voice, and risk-payoff pairings per the progressive explanation pattern
- Updated `docs/concepts/choose-the-right-tool.md` — removed 4 "future" qualifiers from DAPR FAQ references, updated present tense, and aligned DAPR version/runtime wording with the actual repository state
- Cleaned up `.lycheeignore` — removed 2 dapr-faq dead-link exclusion lines
- Cleaned up `lychee.toml` — removed `guides/dapr-faq` from exclusion list and added a targeted exclusion for the README docs-validation workflow badge false positive
- Updated `README.md` — added DAPR FAQ Deep Dive link to Guides section
- All markdownlint-cli2 checks pass with 0 errors
- Story-scoped lychee validation passes with 0 errors for `docs/guides/dapr-faq.md`, `docs/concepts/choose-the-right-tool.md`, and `README.md`
- Senior review fixes corrected DAPR version drift, package-boundary wording, and container-runtime overstatement in the DAPR docs

### Implementation Plan

Documentation-only story following the progressive DAPR explanation pattern. Created the deepest-level FAQ page with question-answer format, TL;DR blockquotes, deep analysis sections, and honest competitive context. Every section references existing docs (choose-the-right-tool.md, architecture-overview.md) for background then provides new operational/risk/quantitative content. No paragraphs duplicated from existing pages.

### Change Log

- 2026-03-10: Created DAPR FAQ Deep Dive page, updated cross-references, cleaned up link exclusions, updated README
- 2026-03-10: Senior Developer Review (AI) found 1 CRITICAL, 2 HIGH, and 1 MEDIUM documentation issues; status kept at `review` pending remediation.
- 2026-03-10: Addressed review findings — corrected DAPR version/package-boundary wording, clarified container-runtime guidance, added focused lychee validation evidence, and moved story status to `done`.

### File List

- `docs/guides/dapr-faq.md` — NEW: DAPR FAQ Deep Dive page (9 FAQ sections + risk assessment table)
- `docs/concepts/choose-the-right-tool.md` — MODIFIED: removed "future" qualifiers and corrected DAPR version/runtime-boundary wording
- `.lycheeignore` — MODIFIED: removed dapr-faq dead-link exclusion lines
- `lychee.toml` — MODIFIED: removed dapr-faq from exclusion list and excluded unauthenticated GitHub workflow badge false positives used by README validation
- `README.md` — MODIFIED: added DAPR FAQ Deep Dive link to Guides section
- `_bmad-output/implementation-artifacts/15-6-dapr-faq-deep-dive.md` — MODIFIED: review remediation notes, corrected validation evidence, and status sync
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — MODIFIED: story status synced to `done`

## Senior Developer Review (AI)

### Reviewer

GitHub Copilot (GPT-5.4)

### Date

2026-03-10

### Outcome

Approved

### Summary

- Corrected conflicting DAPR SDK version claims to match `Directory.Packages.props` (`1.16.1`).
- Rewrote the package-isolation wording to distinguish DAPR-free domain code from DAPR-aware runtime/hosting helpers.
- Clarified that the documented deployment path is container-first while acknowledging Dapr slim mode for local development.
- Replaced the non-working `npx lychee` validation claim with reproducible native CLI evidence and documented that repo-wide docs checks still fail outside this story's scope.

### Findings

1. **[RESOLVED] Validation command mismatch** — `npx lychee --config lychee.toml docs/` was not a runnable command in this environment; validation now uses the installed `lychee.exe` CLI with recorded results.
2. **[RESOLVED] DAPR version drift** — the FAQ and comparison guide now consistently report the pinned SDK version as `1.16.1`.
3. **[RESOLVED] Package-boundary overstatement** — docs now describe the true boundary: domain code is DAPR-free, while supporting runtime/hosting helpers still contain DAPR-facing references.
4. **[RESOLVED] Container-runtime overstatement** — docs now describe the container-first documented path while acknowledging local Dapr slim mode.

### Follow-up Notes

- `lychee --config lychee.toml docs/` still reports pre-existing failures outside Story 15-6 scope, mainly in `docs/reference/api/`, `docs/community/`, and one stale external Dapr docs URL in `docs/guides/troubleshooting.md`.
- Story acceptance criteria are satisfied, markdownlint passes, and story-scoped link validation passes.
