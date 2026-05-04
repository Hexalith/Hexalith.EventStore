---
stepsCompleted: ['step-01-load-context', 'step-02-discover-tests', 'step-03-quality-evaluation', 'step-03f-aggregate-scores']
lastStep: 'step-03f-aggregate-scores'
lastSaved: '2026-05-04'
status: 'in-progress'
workflowType: 'testarch-test-review'
reviewScope: 'suite'
priorReview: 'archive/test-review-2026-04-18.md'
inputDocuments:
  - '.claude/skills/bmad-testarch-test-review/resources/knowledge/test-quality.md'
  - '.claude/skills/bmad-testarch-test-review/resources/knowledge/test-levels-framework.md'
  - '.claude/skills/bmad-testarch-test-review/resources/knowledge/data-factories.md'
  - '.claude/skills/bmad-testarch-test-review/resources/knowledge/selective-testing.md'
  - '.claude/skills/bmad-testarch-test-review/resources/knowledge/test-healing-patterns.md'
  - '.claude/skills/bmad-testarch-test-review/resources/knowledge/selector-resilience.md'
  - '.claude/skills/bmad-testarch-test-review/resources/knowledge/timing-debugging.md'
  - 'CLAUDE.md'
  - '_bmad-output/test-artifacts/archive/test-review-2026-04-18.md (prior review for context)'
---

# Test Quality Review: Hexalith.EventStore (Full Suite)

**Review Date**: 2026-05-04
**Reviewer**: Murat (TEA Agent)
**Review Scope**: suite — full .NET 10 test estate
**Stack Detected**: fullstack (.NET 10 / xUnit / NSubstitute / Shouldly / Microsoft.Playwright .NET binding / bUnit Blazor)
**Prior Review**: 2026-04-18 — 88/100, archived to `archive/test-review-2026-04-18.md` (fresh review per user request, not a delta)

---

## Step 1 — Context Load Summary

### Stack Detection
- `test_stack_type: auto` → resolved to **fullstack** (.NET backend dominant + Blazor UI surface).
- Top-level `package.json` present but **only** for `semantic-release` (no frontend deps).
- E2E framework: `Microsoft.Playwright` (NuGet, .NET binding) — no Node.js Playwright/Cypress.
- Knowledge profile: core fragments only; Playwright Utils + Pact.js Utils fragments **deliberately skipped** (Node.js-specific, not applicable).

### Knowledge Fragments Loaded (Core Tier)
- `test-quality.md` — DoD: deterministic, isolated, explicit, focused, fast (<1.5 min, <300 LOC, no hard waits, self-cleaning, parallel-safe).
- `test-levels-framework.md` — unit / integration / E2E selection rules; duplicate-coverage guard.
- `data-factories.md` — factory-with-overrides, API-first seeding (10–50× faster than UI), composition.
- `selective-testing.md` — tag/grep, spec filters, diff-based runs, promotion stages.
- `test-healing-patterns.md` — failure-signature catalog (stale selector, race, dynamic data, network, hard wait).
- `selector-resilience.md` — `data-testid > ARIA > text > CSS/ID` hierarchy.
- `timing-debugging.md` — network-first, deterministic waits, no `Thread.Sleep`/`Task.Delay` in tests.

### Project Context Artifacts Found
- **CLAUDE.md** — defines Tier 1 / Tier 2 / Tier 3 structure, code review rules, integration-test rule (state-store end-state assertions, not just status codes), ULID-only ID validation rule.
- **80+ implementation artifacts** under `_bmad-output/implementation-artifacts/` (Epics 1-20 stories) — too granular for direct review consumption; consulted opportunistically.
- **Prior review** — gives 14 test projects / 516 test files / 47.2k LOC baseline (subject to recount in step 2).
- **NFR assessment** (`nfr-assessment.md`, 2026-04-18) — out of scope for this review (route to NR skill).
- **Trace coverage** (`tea-trace-coverage-matrix-2026-04-18.json`) — out of scope (route to TR skill).

### Coverage Mapping Out of Scope
Per the workflow, coverage gaps and gate decisions are **not** part of test-review. They route to the **trace** skill.


---

## Step 2 — Test Discovery & Structure

### Suite Inventory

| Metric | Value |
|---|---|
| Test source files (`*.cs` under `tests/`, excluding obj/bin) | **632** |
| Total test LOC | **~44,714** |
| Total test methods (`[Fact]` + `[Theory]`) | **~4,297** |
| Test projects | **15** |
| Largest project (Server.Tests) | 169 files / 40,494 LOC / 1,560 methods |

### Per-Project Footprint

| Project | Files | LOC | Methods (Fact+Theory) | Tier |
|---|---:|---:|---:|---|
| Hexalith.EventStore.Server.Tests | 169 | 40,494 | 1,560 | T2 |
| Hexalith.EventStore.Admin.UI.Tests | 85 | 13,044 | 596 | T1 (bUnit) |
| Hexalith.EventStore.IntegrationTests | 68 | 12,359 | 231 | T3 (Aspire) |
| Hexalith.EventStore.Admin.Server.Tests | 58 | 9,269 | 455 | T1/T2 |
| Hexalith.EventStore.Admin.Cli.Tests | 50 | 5,796 | 255 | T1 |
| Hexalith.EventStore.Admin.Mcp.Tests | 31 | 3,903 | 250 | T1 |
| Hexalith.EventStore.Client.Tests | 15 | 4,847 | 291 | T1 |
| Hexalith.EventStore.Admin.Abstractions.Tests | 52 | 3,110 | 257 | T1 |
| Hexalith.EventStore.Contracts.Tests | 24 | 2,234 | 202 | T1 |
| Hexalith.EventStore.Sample.Tests | 8 | 1,106 | 63 | T1 |
| Hexalith.EventStore.Testing.Tests | 11 | 1,050 | 78 | T1 |
| Hexalith.EventStore.SignalR.Tests | 1 | 648 | 35 | T1 |
| Hexalith.EventStore.Admin.Server.Host.Tests | 2 | 370 | 15 | T1 |
| Hexalith.EventStore.Admin.UI.E2E | 6 | 358 | 9 | E2E |
| Hexalith.EventStore.TestSubscriber | 1 | 188 | 0 (helper, not test project) | helper |

Plus **Hexalith.Tenants** submodule tests (10+ files, ~5K LOC) including the largest test file in the codebase — `TenantAggregateTests.cs` at 1,455 LOC.

### Top 10 Oversized Files (>300 LOC violates DoD)

| LOC | File |
|---:|---|
| 1,455 | `Hexalith.Tenants/.../TenantAggregateTests.cs` |
| 1,169 | `Hexalith.Tenants/.../TenantConformanceTests.cs` |
| **1,109** | `Client.Tests/Aggregates/EventStoreAggregateTests.cs` (was 1,030 in prior review — **grew +79 LOC**) |
| 934 | `Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs` |
| 813 | `Server.Tests/Observability/DeadLetterOriginTracingTests.cs` |
| 808 | `Server.Tests/Actors/EventDrainRecoveryTests.cs` |
| 793 | `IntegrationTests/ContractTests/QueryCacheTopologyProofE2ETests.cs` |
| 771 | `Server.Tests/Controllers/QueriesControllerTests.cs` |
| 753 | `Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs` |
| 709 | `Server.Tests/Actors/CachingProjectionActorTests.cs` |

(17 more files between 300–700 LOC.)

### Framework / Infrastructure Signals

- **xUnit fixture infrastructure**: 56 occurrences of `IClassFixture` / `ICollectionFixture` / `IAsyncLifetime` / `[CollectionDefinition]` across 45 files. Strong investment in proper isolation.
- **Microsoft.Playwright (.NET binding)**: used in `Admin.UI.E2E` only (6 files); custom Kestrel + Chromium fixture (`PlaywrightFixture.cs`).
- **bUnit Blazor component testing**: `Admin.UI.Tests` (85 files, 596 methods) — heavy renderer usage.
- **DAPR test containers**: `DaprTestContainerFixture.cs` (Tier 2 Server.Tests).
- **Aspire E2E topology**: `AspireTopologyFixture.cs`, `AspireContractTestFixture.cs`, `AspirePubSubProofTestFixture.cs`, `AspireProjectionFaultTestFixture.cs`, `KeycloakAuthFixture.cs` (Tier 3 IntegrationTests).

### Trait Tagging (Selective Test Execution)

| Trait | Count |
|---|---:|
| `[Trait("Category", "E2E")]` | 26 |
| `[Trait("Tier", "3")]` | 22 |
| `[Trait("Tier", "2")]` | 20 |
| `[Trait("Category", "Integration")]` | 23 |
| `[Trait("Tier", "1")]` | 3 |
| `[Trait("Priority", ...)]` | **2 only** (P0, P1) |
| Other (Feature, Configuration) | 3 |

Total **~98 trait declarations** across ~4,300 methods → **~2.3% tagging coverage**. Tier/Category tagging is decent for the integration and E2E suites; **Priority tagging (P0/P1/P2/P3) is essentially absent** — limits selective test execution by risk.

### Anti-Pattern Scan

| Pattern | Count | Status |
|---|---:|---|
| `Thread.Sleep(...)` | **2** | ⚠️ **REGRESSION** — `PubSubDeliveryProofTests.cs:244,266` (50ms inside file-IO retry loops; should be `Task.Delay` even when called sync) |
| `Task.Delay(TimeSpan.FromSeconds(>=10))` (real-time, non-poll) | **3** | ⚠️ **PERSISTENT** — `DaprHealthHistoryCollectorTests.cs:71,101,145` (51s wall-clock, **same finding as 2026-04-18 review**) |
| `Task.Delay` inside polling loops with deadline | many | ✅ acceptable — bounded poll intervals |
| `Task.Delay` 3–5s in `ChaosResilienceTests.cs` | 6 | ✅ intentional chaos simulation, justified |
| `.Result` / `.Wait()` / `.GetAwaiter().GetResult()` (sync-over-async) | **0** | ✅ clean (prior review's `AdminApiClientPostTests.cs:67` resolved) |
| `async void` test methods | **0** | ✅ clean |
| Reflection (`BindingFlags.NonPublic`, `GetMethod`/`GetProperty`/`GetField`) | 30+ files | ⚠️ **DRY violation** — `typeof(Actor).GetProperty("StateManager")` repeated in **15+ files**; SignalR client tests reach into private members for `OnProjectionChanged`/`OnReconnectedAsync`/`OnClosedAsync`/`ReconnectConfigurator` |
| `try { ... } catch` blocks | 95 in 27 files | ⚠️ mostly defensive cleanup; `EventStoreSignalRClientTests.cs` has 28 — needs scrutiny in step 3 |

### Discovery Halt Check

✅ Tests found, scope `suite` valid, ready for quality evaluation.


---

## Step 3 — Quality Evaluation (4 Parallel Subagents)

Execution mode: **subagent (parallel)** — 4 dimensions evaluated concurrently. JSON outputs in `_bmad-output/test-artifacts/.tea-tmp/`.

### Per-Dimension Scores

| Dimension | Score | Grade | Weight | Contribution |
|---|---:|---|---:|---:|
| Determinism | **0** | F | 0.30 | 0.0 |
| Isolation | **76** | C | 0.30 | 22.8 |
| Maintainability | **18** | F | 0.25 | 4.5 |
| Performance | **73** | C | 0.15 | 10.95 |
| **Overall (weighted)** | **38** | **F** | 1.00 | **38.25** |

### Violation Counts (Aggregated)

| Severity | Count |
|---|---:|
| HIGH | 31 |
| MEDIUM | 14 |
| LOW | 9 |
| **Total** | **54** |

### ⚠️ Important Scoring Caveat

The 38/100 raw score is the mechanical output of `100 - sum(severity_weight × count)` per dimension and **does not size-normalize against the 4,300-method suite**. Determinism's 0 is driven by 10 concentrated HIGH violations (4 of which are the same `DaprHealthHistoryCollector` test class repeated, and the others are wall-clock assertions in 3 files). Maintainability's 18 is dominated by 16 oversized files (largely Tier 2 integration tests where 700–1,500 LOC reflects DAPR-actor/projection complexity, not cargo-culting). The qualitative picture is closer to B/B+ — strong xUnit fixture infrastructure, zero sync-over-async, zero `async void`, mostly bounded polling — but with concentrated, high-leverage issues that the unweighted penalty model amplifies. **Treat the per-issue findings (Step 4) as authoritative; treat the aggregate score as directional.**

