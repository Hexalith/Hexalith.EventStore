---
storyId: post-epic-deferred-dw2-admin-dapr-mcp-live-evidence
storyKey: post-epic-deferred-dw2-admin-dapr-mcp-live-evidence
storyFile: _bmad-output/implementation-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence.md
atddChecklistPath: _bmad-output/test-artifacts/atdd-checklist-post-epic-deferred-dw2-admin-dapr-mcp-live-evidence.md
detectedStack: backend
testFramework: xunit-v3
inputDocuments:
  - _bmad-output/implementation-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence.md
  - tests/Hexalith.EventStore.Admin.Server.Tests/Hexalith.EventStore.Admin.Server.Tests.csproj
  - tests/Hexalith.EventStore.Admin.Mcp.Tests/Hexalith.EventStore.Admin.Mcp.Tests.csproj
  - tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj
  - .claude/skills/bmad-testarch-atdd/resources/tea-index.csv
  - _bmad/tea/config.yaml
  - knowledge:data-factories
  - knowledge:test-quality
  - knowledge:test-healing-patterns
  - knowledge:test-levels-framework
  - knowledge:test-priorities-matrix
  - knowledge:ci-burn-in
generatedTestFiles: []
stepsCompleted:
  - step-01-preflight-and-context
  - step-02-generation-mode
  - step-03-test-strategy
  - step-04-generate-tests
  - step-05-validate-and-complete
lastStep: step-05-validate-and-complete
lastSaved: 2026-05-05
generationMode: ai-generation
generatedTestFiles:
  - tests/Hexalith.EventStore.Admin.Server.Tests/Services/Dw2RemoteMetadataPerSurfaceAtddTests.cs
  - tests/Hexalith.EventStore.Admin.Server.Tests/Services/Dw2DebuggingTimeoutAtddTests.cs
  - tests/Hexalith.EventStore.Admin.Server.Tests/Evidence/Dw2EvidenceIndexAtddTests.cs
  - tests/Hexalith.EventStore.Admin.Mcp.Tests/Dw2McpProtocolGatesAtddTests.cs
totalScaffolds: 26
buildVerified: true
runtimeVerified: true
---

# ATDD Red-Phase Checklist — DW2 Admin DAPR MCP Live Evidence

## Step 01 — Preflight & Context

### Stack Detection
- Detected: `backend` (.NET 10, xUnit v3, Shouldly, NSubstitute)
- No `playwright.config.*` or JS frontend manifests at repo root → backend profile
- Loading profile: backend-only knowledge fragments (matches DW1 precedent)

### Prerequisites
- [x] Story has clear acceptance criteria (15 ACs covering runtime baseline, Admin DAPR, Epic 20 debugging, MCP, latency, evidence/disposition bookkeeping)
- [x] Test framework configured: `Hexalith.EventStore.Admin.Server.Tests.csproj`, `Hexalith.EventStore.Admin.Mcp.Tests.csproj`, and `Hexalith.EventStore.IntegrationTests.csproj` reference xunit.v3, Shouldly, NSubstitute
- [x] Dev environment available (.NET SDK 10.0.103 pinned)

### Story Identity
- `story_key` = `post-epic-deferred-dw2-admin-dapr-mcp-live-evidence`
- `story_id` = `post-epic-deferred-dw2-admin-dapr-mcp-live-evidence` (no `epic.story` numbering for deferred-work cluster)
- Story status: `ready-for-dev`
- Source HEAD at story creation: `41fc73da`

### Story Type — Evidence/Closure (not feature)
DW2 is an **evidence-and-closure** story. Most ACs are runtime smoke evidence captured into `_bmad-output/test-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence/`. ATDD red-phase scaffolds therefore concentrate on:

1. **Behavior/contract gates that must hold** when the evidence smoke runs (parser determinism, RemoteMetadataStatus surfaces, MCP stdout/stderr discipline, debugging-tool timeout classifications, latency-sample metadata shape).
2. **Evidence-artifact completeness gates** (index format, blocker taxonomy, redaction policy, deferred-work disposition markers) — verifiable without running Aspire.

Tests that require live Aspire/Docker/DAPR runtime stay `Tier 3` and follow existing `AspireTopologyFixture` patterns; tests that exercise pure parsers/services stay `Tier 1` (no Docker).

### Target Production Files (read-only context for scaffolds)
- `src/Hexalith.EventStore.AppHost/Program.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminDaprController.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminHealthController.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthHistoryCollector.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminTracesController.cs`
- `src/Hexalith.EventStore.Admin.Mcp/Program.cs`
- `src/Hexalith.EventStore.Admin.Mcp/Tools/*.cs`

### Target Test Projects (extend, not create)
- `tests/Hexalith.EventStore.Admin.Server.Tests/` — Tier 1 unit/contract tests for parser & remote-metadata behavior
- `tests/Hexalith.EventStore.Admin.Mcp.Tests/` — Tier 1 tests for MCP host startup gates and protocol discipline
- `tests/Hexalith.EventStore.IntegrationTests/` — Tier 3 Aspire-runtime gate(s), e.g. `RemoteMetadataStatus` matrix proof if appropriate

### TEA Config Flags Loaded
- `tea_use_playwright_utils: true` — N/A for backend stack
- `tea_use_pactjs_utils: true` — not applicable (no contract testing in DW2 scope)
- `tea_pact_mcp: mcp` — irrelevant; DW2 MCP is the project's own Admin.Mcp host, not Pact MCP
- `tea_browser_automation: auto` — N/A for backend stack
- `test_stack_type: auto` → resolved to `backend`

### Knowledge Fragments Loaded
- Core: `data-factories.md`, `test-quality.md`, `test-healing-patterns.md`
- Backend: `test-levels-framework.md`, `test-priorities-matrix.md`, `ci-burn-in.md`

### Scope Guards (from story Scope Boundaries)
- DO NOT extend tests into DW3 JSON reconstruction or large-stream hardening behavior
- DO NOT validate the DW4 evidence-template schema (DW4 owns it)
- DO NOT add Admin UI a11y/visual tests beyond what the existing E2E project already covers
- DO NOT change DAPR component YAML, access-control YAML, or publish overlays
- DO NOT add new MCP tools to satisfy a test gate; lock down existing tool contracts only
- DO NOT run nested submodule init

## Step 02 — Generation Mode

**Mode**: `ai-generation`

**Why**: Backend stack (.NET 10), no UI scenarios in DW2 scope. Story acceptance criteria are concrete and source-anchored (parser shapes, controller surfaces, MCP protocol contracts). No browser recording needed; matches DW1 precedent.

### Definition of "Red-Phase Pass"
A scaffold is acceptable if:
1. It fails (or is `Skip`-marked with the AC reference) until production code or evidence artifacts make it pass.
2. The skip reason names the AC and the action required ("DW2 AC #N — implement/record X").
3. It does not duplicate an existing assertion already enforced elsewhere.
4. It uses the canonical seeded-stream identifier block (tenant/domain/aggregate id/correlation id) when targeting Epic 20 debugging or MCP smoke surfaces.

## Step 03 — Test Strategy

### Acceptance Criteria → Test Coverage Map

| AC | Behavior | Level | Priority | Automated? | Target file |
|---:|---|---|:---:|:---:|---|
| 1 | Aspire runtime baseline captured | manual smoke | P2 | NO | evidence index only |
| 2 | Admin DAPR component evidence covers runtime surface | unit + manual smoke | P1 | YES | `Dw2RemoteMetadataPerSurfaceAtddTests.cs` (lock contract) |
| 3 | Remote sidecar metadata status proven (per-surface) | unit | P0 | YES | `Dw2RemoteMetadataPerSurfaceAtddTests.cs` |
| 4 | DAPR Admin evidence does not hide degraded states | unit | P0 | YES | `Dw2RemoteMetadataPerSurfaceAtddTests.cs` |
| 5 | Epic 20 debugging tools smoke-tested (timeout discipline) | unit + manual smoke | P1 | YES | `Dw2DebuggingTimeoutAtddTests.cs` |
| 6 | Debugging evidence respects scope limits (DW3 not absorbed) | review-only | n/a | NO | (rule, no test) |
| 7 | MCP startup and protocol smoke (stdio discipline) | unit (process) | P0 | YES | `Dw2McpProtocolGatesAtddTests.cs` |
| 8 | Representative MCP read + write-preview tools (non-mutation) | unit | P1 | YES | `Dw2McpProtocolGatesAtddTests.cs` |
| 9 | MCP session fallback (absent / broken / blocked classification) | unit | P1 | YES | `Dw2McpProtocolGatesAtddTests.cs` |
| 10 | NFR43 MCP latency plan + sample | manual evidence | P2 | NO | evidence index only |
| 11 | Evidence artifacts durable & reviewable (index + tables) | structural | P0 | YES | `Dw2EvidenceIndexAtddTests.cs` |
| 12 | Deferred-work dispositions updated narrowly | structural | P1 | YES | `Dw2EvidenceIndexAtddTests.cs` |
| 13 | Security & redaction preserved | review-only | n/a | NO | (rule, no test) |
| 14 | No deployment topology / contract changes | review-only | n/a | NO | (rule, no test) |
| 15 | Bookkeeping closed (Dev Agent Record, status) | review-only | n/a | NO | (rule, no test) |

### Test Levels Used

- **Unit (Tier 1)** — pure parser/service/protocol behavior. NSubstitute mocks for `DaprClient`, `IHttpClientFactory`, `AdminApiClient`. No Docker, no DAPR.
- **Process unit (Tier 1)** — out-of-process MCP host launch with `Process.Start(dotnet ...)` (precedent: `ConfigurationValidationTests.cs`). Short timeout windows; stdout/stderr captured.
- **Structural** — file/markdown asserts on `_bmad-output/test-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence/` index. No build dependency.
- **No Tier 2/Tier 3** — DW2 is evidence-and-closure. Live Aspire runtime gates are the evidence smoke itself, not automated tests in this scaffold.

### Test File Plan (4 new files)

1. **`tests/Hexalith.EventStore.Admin.Server.Tests/Services/Dw2RemoteMetadataPerSurfaceAtddTests.cs`** (AC #2, #3, #4) — 8 scaffolds
   - sidecar/actors/pubsub each emit own `RemoteMetadataStatus` (per-surface independence)
   - `NotConfigured` when endpoint blank (sidecar/actors/pubsub)
   - `Unreachable` when endpoint configured but HTTP fails (sidecar/actors/pubsub)
   - pub/sub `rules[]` direct-array AND `rules.rules[]` wrapped-object both yield route
   - resiliency parser surfaces `NotFound`/`ReadError`/`ParseError` distinctly with `IsConfigurationAvailable=false`

2. **`tests/Hexalith.EventStore.Admin.Server.Tests/Services/Dw2DebuggingTimeoutAtddTests.cs`** (AC #5) — 5 scaffolds
   - blame uses 30 s service-invocation timeout
   - step-through uses 30 s timeout
   - trace-map uses 30 s timeout
   - bisect uses 60 s timeout
   - timeout produces a stable classification distinct from "empty success"

3. **`tests/Hexalith.EventStore.Admin.Mcp.Tests/Dw2McpProtocolGatesAtddTests.cs`** (AC #7, #8, #9) — 7 scaffolds
   - stdout silent before MCP initialize when valid env vars supplied (no log bleed-through)
   - all logging goes to stderr (verified by absence of log lines on stdout)
   - write-preview tools (`consistency-trigger`/`consistency-cancel`/`projection-*`/`backup-*`) when `confirm=false` MUST NOT call `AdminApiClient`
   - write-preview shape is stable (`mode: preview`, `nextStep`, `payload`, no mutation flag)
   - tools/list-discoverable: at least one `ping`/`health-status` tool, one approval-gated write tool, `session-set-context`, `session-get-context`
   - `SessionTools.SetContext` followed by scope-omitted call uses session values (positive)
   - session classification helpers: `feature absent` vs `feature broken` vs `blocked by missing session-establishment`

4. **`tests/Hexalith.EventStore.Admin.Server.Tests/Evidence/Dw2EvidenceIndexAtddTests.cs`** (AC #11, #12) — 5 scaffolds
   - evidence folder `_bmad-output/test-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence/` exists
   - index file under that folder contains required tables (runtime baseline, Admin DAPR, Epic 20 debugging, MCP, latency, blockers, dispositions, how-to-rerun)
   - `RemoteMetadataStatus` matrix has separate rows for sidecar / actors / pubsub
   - canonical seeded-stream identifier block appears across Admin API, Admin UI, MCP, CommandAPI artifacts
   - `_bmad-output/implementation-artifacts/deferred-work.md` touched bullets carry a `STORY:post-epic-deferred-dw2-admin-dapr-mcp-live-evidence` or `RESOLVED`/`ACCEPTED-DEBT`/`DUPLICATE`/`NO-ACTION` marker

**Total scaffolds**: 25 (matches DW1 cardinality)

### Red-Phase Strategy (per DW1 precedent)

- All 25 facts marked `[Fact(Skip = "ATDD red phase — DW2 AC#N (description). Remove Skip when implementing/evidence captured.")]`
- Skip strings name the exact AC number and the action that unmarks the test
- Build must succeed with all tests in `Skipped` state
- Dev removes Skip per AC as work lands or evidence is captured. For locks (already-implemented behavior), the dev unmarks once the live smoke evidence confirms the behavior is observed at runtime

### Canonical Identifier Block (for cross-surface tests)

Per the story's Cross-Surface Consistency Rules, the seeded-stream identifier block reused across Epic 20 debugging and MCP scaffolds:

```text
TenantId       = test-tenant-dw2
Domain         = counter
AggregateId    = 01HXDW2COUNTER0000000001
EventCount     = 5
CorrelationId  = 01HXDW2CORR000000000001
```

These constants are placeholders for the seeded smoke. Tests assert structural shape, not exact values.

## Step 04 — Generate Tests (Sequential, single-pass)

Mode: `sequential` — backend-only stack, no E2E, no subagent split. Matches DW1 precedent.

### Files Created (4 new)

| File | Scaffolds | ACs covered |
|---|:---:|---|
| `tests/Hexalith.EventStore.Admin.Server.Tests/Services/Dw2RemoteMetadataPerSurfaceAtddTests.cs` | 8 | #2, #3, #4 |
| `tests/Hexalith.EventStore.Admin.Server.Tests/Services/Dw2DebuggingTimeoutAtddTests.cs` | 5 | #5 |
| `tests/Hexalith.EventStore.Admin.Server.Tests/Evidence/Dw2EvidenceIndexAtddTests.cs` | 5 | #11, #12 |
| `tests/Hexalith.EventStore.Admin.Mcp.Tests/Dw2McpProtocolGatesAtddTests.cs` | 8 | #7, #8, #9 |
| **Total** | **26** | |

### Build Verification (Release)

```text
tests/Hexalith.EventStore.Admin.Server.Tests   → Build succeeded. 0 Warning(s) 0 Error(s)
tests/Hexalith.EventStore.Admin.Mcp.Tests      → Build succeeded. 0 Warning(s) 0 Error(s)
```

### Runtime Verification (filter: `~Dw2`)

```text
Admin.Server.Tests  → Skipped: 18, Failed: 0, Passed: 0, Total: 18 (6 ms)
Admin.Mcp.Tests     → Skipped:  8, Failed: 0, Passed: 0, Total:  8 (5 ms)
```

All 26 facts in `[Fact(Skip = "...")]` red-phase state. No fact executed; no fail; no pass.

### TDD Compliance (red-phase contract)

- All 26 scaffolds use `[Fact(Skip = "...")]`
- Every Skip string names the AC and the action that unmarks it
- No active passing tests added
- Scaffolds reference real types (no fictional APIs); compiler-verified
- Canonical seeded-stream identifier block is hard-coded once and reused across MCP/debugging files

### Issues Found and Fixed During Build

| # | Issue | File | Fix |
|---:|---|---|---|
| 1 | `GetBlameViewAsync` (does not exist) | `Dw2DebuggingTimeoutAtddTests.cs` | renamed to actual method `GetAggregateBlameAsync` |
| 2 | `GetCorrelationTraceMapAsync` missing required `domain` arg | `Dw2DebuggingTimeoutAtddTests.cs` | added `domain` and `aggregateId` parameters |
| 3 | NSubstitute `Returns(throw)` ambiguity | `Dw2RemoteMetadataPerSurfaceAtddTests.cs` | switched to `ThrowsAsync` (NSubstitute.ExceptionExtensions) |
| 4 | `DaprComponentDetail.Name` / `.Health` (do not exist) | `Dw2RemoteMetadataPerSurfaceAtddTests.cs` | renamed to `ComponentName` / `Status` |
| 5 | xunit1031 (blocking task ops in test) | `Dw2McpProtocolGatesAtddTests.cs` | switched to async `ReadToEndAsync(ct)` + `WaitForExitAsync` |
| 6 | xunit1030 (`ConfigureAwait(false)` in test) | `Dw2McpProtocolGatesAtddTests.cs` | removed `.ConfigureAwait(false)` calls |
| 7 | CS8122 (`is` pattern in expression-tree predicate) | `Dw2McpProtocolGatesAtddTests.cs` | replaced `ShouldContain(name => name is "x" or …)` with `Any(...)+Array.IndexOf` |

## Step 05 — Validate & Complete

### Checklist Validation

- [x] Prerequisites satisfied (story has 15 ACs, xUnit v3 framework configured, .NET 10 SDK pinned)
- [x] 4 test files created at the planned paths
- [x] Test files match acceptance criteria mapping in Step 03
- [x] All 26 facts marked `[Fact(Skip = "...")]` (TDD red phase) — verified at runtime
- [x] Story metadata captured: `storyId`, `storyKey`, `storyFile`, `atddChecklistPath`, `generatedTestFiles`
- [x] No CLI sessions or browser orphans (backend stack — N/A)
- [x] Artifacts under `_bmad-output/test-artifacts/` (this checklist file) — not random locations
- [x] Build clean for both target test projects
- [x] No tests executed (all skipped)
- [x] Skip strings name the AC + unmark trigger

### Completion Summary

**Test files created (4)**:

- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/Dw2RemoteMetadataPerSurfaceAtddTests.cs` — 8 scaffolds (AC #2, #3, #4)
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/Dw2DebuggingTimeoutAtddTests.cs` — 5 scaffolds (AC #5)
- `tests/Hexalith.EventStore.Admin.Server.Tests/Evidence/Dw2EvidenceIndexAtddTests.cs` — 5 scaffolds (AC #11, #12)
- `tests/Hexalith.EventStore.Admin.Mcp.Tests/Dw2McpProtocolGatesAtddTests.cs` — 8 scaffolds (AC #7, #8, #9)

**Total**: 26 red-phase facts. Build clean. Runtime: 26 skipped / 0 failed / 0 passed.

**Checklist output path**: `_bmad-output/test-artifacts/atdd-checklist-post-epic-deferred-dw2-admin-dapr-mcp-live-evidence.md`

**Story handoff path**: `_bmad-output/implementation-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence.md`

### Key Risks and Assumptions

1. **DW2 is evidence/closure, not feature** — most "Skip removal" actions are evidence captures (live MCP transcript, DAPR runtime smoke, evidence index/dispositions), not production-code edits. The dev removes Skip per AC after the corresponding evidence row is recorded.
2. **Wall-clock timeout tests are intentionally heavy** — `Dw2DebuggingTimeoutAtddTests.cs` includes wall-clock bounds against the 30s/60s caps. After the dev removes Skip, those tests will add ~30+ seconds to CI; recommend running them as an opt-in suite, not on every PR.
3. **Stdio discipline test launches `dotnet`** — `McpHost_ValidEnvVars_KeepsStdoutEmptyBeforeJsonRpc` follows the existing `ConfigurationValidationTests` pattern. Requires the Admin.Mcp project to be built before tests execute.
4. **Evidence-folder gates depend on filesystem layout** — `Dw2EvidenceIndexAtddTests` resolves the repo root from the test assembly location. Validates only after the smoke creates the evidence folder per Task 0.2 of the story.
5. **AC #1, #6, #10, #13–#15 are review-only** — no automated scaffold. The story explicitly designates them as manual smoke / review rules; the AC → Test Coverage Map in Step 03 records this transparently.

### Next Recommended Workflow

→ **`/bmad-dev-story` for `post-epic-deferred-dw2-admin-dapr-mcp-live-evidence`**

The dev workflow:

1. Runs Aspire smoke per Task 1 (capture runtime baseline → Skip removal for AC#1, #11 evidence-folder/index gates).
2. Exercises Admin DAPR endpoints (Task 2) and removes `[Fact(Skip)]` from `Dw2RemoteMetadataPerSurfaceAtddTests.cs` once the per-surface RemoteMetadataStatus matrix row is written.
3. Seeds the canonical stream (Task 3.1) and exercises Epic 20 tools (Task 3.2) — removes Skip from `Dw2DebuggingTimeoutAtddTests.cs` after timeout-classified rows land in the Epic 20 evidence table.
4. Runs the MCP host smoke (Task 4) — removes Skip from `Dw2McpProtocolGatesAtddTests.cs` after the initialize → tools/list → representative read → write-preview → session-fallback → latency sample transcripts are captured.
5. Updates `deferred-work.md` dispositions narrowly (Task 5.1) — removes Skip from `Dw2EvidenceIndexAtddTests.DeferredWork_HasDw2DispositionMarker_OnAtLeastOneBullet`.
6. Moves story and sprint-status row to `review` only after evidence is saved and blockers are classified (Task 5.4).

After dev-story completes and review signs off, the next workflow is `/bmad-testarch-automate` to broaden coverage if any production fixes were applied during DW2 (per the narrow defect gate).
