---
stepsCompleted: ['step-01-load-context', 'step-02-discover-tests', 'step-03f-aggregate-scores', 'step-04-generate-report']
lastStep: 'step-04-generate-report'
lastSaved: '2026-04-18'
status: 'complete'
workflowType: 'testarch-test-review'
reviewScope: 'suite'
inputDocuments:
  - '_bmad/tea/testarch/knowledge/test-quality.md'
  - '_bmad/tea/testarch/knowledge/test-levels-framework.md'
  - '_bmad/tea/testarch/knowledge/data-factories.md'
  - '_bmad/tea/testarch/knowledge/selector-resilience.md'
  - '_bmad/tea/testarch/knowledge/timing-debugging.md'
  - '_bmad/tea/testarch/knowledge/test-healing-patterns.md'
  - '_bmad/tea/testarch/knowledge/selective-testing.md'
---

# Test Quality Review: Hexalith.EventStore (Full Suite)

**Quality Score**: 88/100 (B+ — Good, actionable issues)
**Review Date**: 2026-04-18
**Review Scope**: suite — 14 test projects, 516 `*Tests.cs` files, 47.2k LOC of test code
**Reviewer**: Murat (TEA Agent)
**Previous Review**: 2026-03-31 (89/100, 541 files)
**Stack Detected**: fullstack (.NET 10 xUnit backend + bUnit Blazor component + Playwright .NET E2E)

---

_Note: This review audits existing tests; it does not generate tests.
Coverage mapping and coverage gates are out of scope here — route those to `trace`._

## Executive Summary

**Overall Assessment**: Good with two actionable P1 issues.

**Recommendation**: **Approve with Fixes** — merge current delta, schedule two P1 items this sprint.

### Score Delta vs. Previous Review

| Dimension        | Prev | Now | Δ   | Note                                                                |
| ---------------- | ---- | --- | --- | ------------------------------------------------------------------- |
| Determinism      | 8.5  | 9.5 | +1  | `Thread.Sleep(2)` in UniqueIdHelperIntegrationTests resolved        |
| Isolation        | 9.5  | 9.5 | 0   | Still excellent — collection fixtures, per-test mocks               |
| Maintainability  | 8.5  | 7.5 | -1  | Reflection-heavy Fluent v5 tests add fragility (Story 21-13 delta)  |
| Performance      | 9.0  | 7.5 | -1.5| `DaprHealthHistoryCollectorTests` — three 17-second real-time waits |
| Coverage Breadth | 9.5  | 9.5 | 0   | Story 21-13 bug regressions added (CommandPalette, TypeCatalog)     |

Net: 89 → 88. Determinism win offset by newly surfaced real-time-delay tests and reflection fragility in Fluent UI v5 component tests.

### Key Strengths

- **Zero `Thread.Sleep` regressions** in test source code — last review's UniqueIdHelper violation is gone.
- **Story 21-13 bug fixes ship with honest regression tests**: `CommandPalette_OpenAsync_DoesNotEarlyReturnWhenIsOpenStuckTrue` and `TypeCatalogPage_DeepLink_SetsActiveTabToAggregatesWithoutRedirectLoop` target exact failure modes with clear diagnostic comments.
- **Consistent naming convention** (`Method_Scenario_Expected`) holds across all 14 projects.
- **Async discipline**: zero `.Wait()`, zero bare `.Result` on domain/production paths (one pragmatic `.Result` in a test mock capture — flagged below).
- **Proper tier discipline** — Tier 1 (unit/component) / Tier 2 (DAPR integration) / Tier 3 (Aspire E2E) boundaries respected.
- **Shouldly + NSubstitute** used consistently; xUnit `IAsyncLifetime` + collection fixtures give clean isolation.
- **Chaos resilience tests** (`ChaosResilienceTests.cs`) model real-world delays correctly — 3s/5s `Task.Delay` is intentional simulation of sidecar disruption.

### Key Weaknesses

- **Real-time `Task.Delay(17s)` ×3** in `DaprHealthHistoryCollectorTests.cs` — 51+ seconds of wall-clock time in a **Tier-1** test class. Blocks the DoD rule "tests <1.5min" and inflates CI.
- **Reflection-access to private Fluent UI v5 dialog state** in `CommandPaletteTests.cs` and `TypeCatalogPageTests.cs`. Honest workaround for a bUnit/JSInterop limitation, but silently breaks on rename.
- **`.Result` sync-over-async** inside a capture lambda in `AdminApiClientPostTests.cs:67`.
- **Weak idempotency assertion** in `TypeCatalogPage_UpdateUrl_IsIdempotent_WhenTargetEqualsCurrentUrl` — `WaitForAssertion(count.ShouldBe(0), 200ms)` passes immediately at t=0, does not prove stability over the window.
- **Large test classes persist** — top file `EventStoreAggregateTests.cs` at 1,030 lines (down from the 1,068-line `AggregateActorTests` noted last time, so decomposition is in progress but not finished).

---

## Findings — Critical (P0)

**None.** No deploy-blocking issues. The recent merges (21-11/12/13) are clean.

---

## Findings — High Priority (P1)

### P1-1. `DaprHealthHistoryCollectorTests` burns 51+ seconds on wall-clock waits

**File**: `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprHealthHistoryCollectorTests.cs:71,101,145`

**Diagnosis**: three test methods each start the hosted collector, then `Task.Delay(TimeSpan.FromSeconds(17))` to wait for the 15-second scheduled capture. This runs in Tier 1 (unit-ish test project).

```csharp
await collector.StartAsync(cts.Token);
await Task.Delay(TimeSpan.FromSeconds(17)); // 15s delay + 2s buffer
await cts.CancelAsync();
```

**Why it matters**: hits every CI run, adds ~1 minute to Tier 1, and the test still *could* be flaky — 2s buffer assumes the scheduler fires promptly. Time-based tests are the classic test-quality DoD violation (`< 1.5 min`, deterministic).

**Fix**: inject `TimeProvider` (or `IScheduler` / `ISystemClock`) into `DaprHealthHistoryCollector` and use a fake time source in the test. `TimeProvider.System` in production, `FakeTimeProvider` (from `Microsoft.Extensions.TimeProvider.Testing`) in tests. Advance virtual time; no wall-clock wait. This pattern already exists idiomatically in .NET 8+; .NET 10 has first-class support.

**Effort**: ~1 hour (refactor collector + rewrite 3 tests). **Impact**: saves ~51s per CI run, removes 3 sources of timing flake.

---

### P1-2. Reflection-access to private Fluent UI v5 dialog state creates silent-rename fragility

**Files**:
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/CommandPaletteTests.cs` — pokes `_isOpen`, `_dialogState`, `_focusSearchRequested`, `_filteredResults`, `_searchQuery` + private method `NavigateToAsync`.
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TypeCatalogPageTests.cs` — pokes `_activeTab` + private methods `OnTabChanged`, `UpdateUrl`.

**Diagnosis**: bUnit's loose JSInterop can't drive Fluent UI v5 dialogs (they render `ChildContent` only after JS opens them), so tests reach into private state via `BindingFlags.NonPublic | BindingFlags.Instance`. The code acknowledges this honestly in comments. However:

- Any private rename turns these tests into runtime `InvalidOperationException` — no compile-time safety.
- Tests effectively couple to implementation, not behaviour.
- If `CommandPalette` is refactored to expose `IsOpen` / `CloseAsync` cleanly, tests would need rewriting anyway.

**Why it matters**: fragility risk scales with every future refactor of these two components. The risk is limited by good failure messages ("Private field 'X' not found on CommandPalette; test assumptions need refresh"), but it's still a "tests are coupled to implementation" smell.

**Fix** (choose one, document the choice):
1. **Preferred** — surface the needed state via `internal` members + `InternalsVisibleTo("Hexalith.EventStore.Admin.UI.Tests")`. Type-safe, survives renames at compile time.
2. **Complement** — cover the JS-dependent paths with **Playwright .NET E2E** in `Admin.UI.E2E` (you already have the fixture). Delete the reflection tests once E2E coverage lands.
3. **Accept** — keep reflection but add a test-helper pair `GetPaletteState(...)` / `SetPaletteState(...)` so the reflection strings live in one place.

**Effort**: ~2 hours for option 1 (the most pragmatic). **Impact**: eliminates a class of silent test breakage.

---

## Findings — Medium Priority (P2)

### P2-1. Sync-over-async in a test mock capture

**File**: `tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientPostTests.cs:67`

```csharp
capturedBody = r.Content!.ReadAsStringAsync().Result; // sync-over-async in a test
```

**Diagnosis**: inside a synchronous capture lambda, `.Result` blocks the thread-pool thread. Low runtime risk in a unit test, but sets a bad precedent for copy-paste. The capture should be restructured to defer reading until after `PostAsync` returns (by then you have the `HttpContent` recorded) or use `GetAwaiter().GetResult()` (no clearer, just louder).

**Fix**: store the `HttpRequestMessage` itself in the capture, read body in the assertion phase:

```csharp
HttpRequestMessage? captured = null;
using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
    r => captured = r,
    HttpStatusCode.OK, _operationResultJson);
// ...act...
captured.ShouldNotBeNull();
string body = await captured!.Content!.ReadAsStringAsync();
body.ShouldContain("42");
```

**Effort**: 15 min. **Impact**: removes one sync-over-async, clearer pattern.

---

### P2-2. Weak idempotency assertion

**File**: `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TypeCatalogPageTests.cs:295-320` (`TypeCatalogPage_UpdateUrl_IsIdempotent_WhenTargetEqualsCurrentUrl`)

**Diagnosis**: the test uses `cut.WaitForAssertion(() => navigationCount.ShouldBe(0), TimeSpan.FromMilliseconds(200));`. `WaitForAssertion` retries until the assertion **passes or the timeout elapses**. Since `navigationCount` starts at 0, the assertion passes on the first poll — the 200ms window is effectively ignored. The test only catches failures if `LocationChanged` fires before the first poll.

**Fix**: to assert "nothing fired during a window," replace with a deterministic post-wait check:

```csharp
await Task.Delay(TimeSpan.FromMilliseconds(200));
navigationCount.ShouldBe(0);
```

Or, better, assert that invoking `UpdateUrl` does not change `nav.Uri` — a state-based check beats a timing-based one:

```csharp
string uriBefore = nav.Uri;
await cut.InvokeAsync(() => updateUrl.Invoke(cut.Instance, new object?[] { false }));
nav.Uri.ShouldBe(uriBefore);
navigationCount.ShouldBe(0);
```

The deep-link sibling test (`TypeCatalogPage_DeepLink_SetsActiveTabToAggregatesWithoutRedirectLoop:291`) uses the plain `navigationCount.ShouldBe(0)` pattern correctly — align this one with that.

**Effort**: 5 min. **Impact**: test actually catches the regression it claims to.

---

### P2-3. Large test classes still exceed DoD line limit

| File                                                          | Lines |
| ------------------------------------------------------------- | ----- |
| `Client.Tests/Aggregates/EventStoreAggregateTests.cs`         | 1,030 |
| `Server.Tests/Observability/DeadLetterOriginTracingTests.cs`  | 813   |
| `Server.Tests/Controllers/QueriesControllerTests.cs`          | 771   |
| `Server.Tests/Actors/CachingProjectionActorTests.cs`          | 709   |
| `Server.Tests/Actors/EventDrainRecoveryTests.cs`              | 682   |
| `Server.Tests/Pipeline/AuthorizationBehaviorTests.cs`         | 676   |

**Diagnosis**: DoD rule in `test-quality.md` says `< 300 lines`. Six files are > 2× the limit. `EventStoreAggregateTests.cs` is > 3×. (Progress noted: previous top offender `AggregateActorTests.cs` at 1,068 lines is no longer in the top-15, so decomposition work is clearly happening.)

**Fix**: split by behaviour area. Typical seams in this codebase:
- Handle-path vs. Apply-path
- Success vs. rejection vs. tombstone
- Validation vs. authorization vs. persistence

Extract shared setup to a collection fixture / private helper, keep each file focused on one concern.

**Effort**: ~1 day per file, incremental. **Impact**: debugging + review time drops materially.

---

### P2-4. Hard-coded magic test strings still present

Carryover from previous review. Strings like `"tenant-a"`, `"acme"`, `"counter"`, `"agg-001"` repeat across test methods rather than being centralized constants. Not a correctness issue, just maintenance drag — when the domain shape shifts, string-by-string sed edits are the tax.

**Fix**: introduce a `TestDomain` static class per test project with canonical identifiers. Low priority.

---

## Findings — Low Priority / Info

- **`DaprTestContainerFixture.cs` uses `Task.Delay(2000)` / `Task.Delay(1000)` / `Task.Delay(200)`** — acceptable. These are polling intervals against a real sidecar boot. The right fix is a readiness probe, but the current pattern is defensible infrastructure code.
- **`ContractTestHelpers.cs` polling loop** — standard Tier-3 polling for eventual consistency, acceptable.
- **`ActorConcurrencyConflictTests.cs` / `EventPersistenceIntegrationTests.cs` `Task.Delay(50)`** — small jitter to spread concurrent writers, intentional and documented.

---

## Per-Project Scorecard

| Project                               | Tier | Files | Score | Notes                                                     |
| ------------------------------------- | ---- | ----- | ----- | --------------------------------------------------------- |
| Contracts.Tests                       | 1    | ~40   | A     | Clean, focused, pure-function oriented                    |
| Client.Tests                          | 1    | ~60   | A-    | Large `EventStoreAggregateTests` (1,030 lines)            |
| Testing.Tests                         | 1    | ~15   | A     | Fakes verified, small focused files                       |
| SignalR.Tests                         | 1    | ~25   | A     | Clean — minimal mocking                                   |
| Sample.Tests                          | 1    | ~10   | A     | Tiny, exemplary                                           |
| Admin.Abstractions.Tests              | 1    | ~20   | A     | Clean contract verification                               |
| Admin.Cli.Tests                       | 1    | ~30   | A-    | `MockHttpMessageHandler` duplicated with Admin.Mcp.Tests  |
| Admin.Mcp.Tests                       | 1    | ~25   | B+    | **P2-1** `.Result` in AdminApiClientPostTests.cs:67       |
| Admin.Server.Tests                    | 1    | ~80   | B     | **P1-1** 17s `Task.Delay` ×3 in DaprHealthHistoryCollector |
| Admin.Server.Host.Tests               | 1    | ~15   | A     | Middleware ordering well covered                          |
| Admin.UI.Tests                        | 1    | ~50   | B+    | **P1-2** reflection-heavy (Story 21-13 delta), **P2-2** weak idempotency assertion |
| Server.Tests                          | 2    | ~130  | A-    | Strong, but several >700-line files                       |
| Admin.UI.E2E                          | 2    | ~6    | A-    | Playwright .NET fixture present; underused for Fluent v5 dialog coverage |
| IntegrationTests                      | 3    | ~40   | A     | Chaos + contract tests well-factored                      |

---

## Remediation Priority & Effort

| # | Finding                                      | Priority | Effort  | CI Impact       |
| - | -------------------------------------------- | -------- | ------- | --------------- |
| 1 | `TimeProvider` in DaprHealthHistoryCollector | P1       | ~1 hr   | -51s/run        |
| 2 | `InternalsVisibleTo` for UI component tests  | P1       | ~2 hrs  | Stability       |
| 3 | Fix idempotency assertion window             | P2       | 5 min   | Correctness     |
| 4 | Remove `.Result` from test mock capture      | P2       | 15 min  | Hygiene         |
| 5 | Split EventStoreAggregateTests (1,030 lines) | P2       | ~1 day  | Maintainability |
| 6 | Centralize magic test strings                | P3       | ~2 hrs  | Maintainability |

**Quick wins (combined < 1 hour)**: #1 + #3 + #4 = biggest signal per minute spent.

---

## Knowledge Base Alignment

| Check (from `test-quality.md` DoD)              | Status                                       |
| ------------------------------------------------- | -------------------------------------------- |
| No Hard Waits (`Thread.Sleep`, arbitrary timeouts)| ⚠️  3 real-time `Task.Delay(17s)` (P1-1)      |
| No Conditionals controlling flow                  | ✅ Pass                                      |
| < 300 lines per test class                        | ❌ 6+ files exceed (P2-3)                    |
| < 1.5 min per test                                | ⚠️ DaprHealthHistoryCollector tests ~17s each|
| Self-cleaning (parallel-safe)                     | ✅ Pass — `IAsyncLifetime` discipline        |
| Explicit assertions (not hidden in helpers)       | ✅ Pass                                      |
| Unique data (no hardcoded IDs)                    | ⚠️  Magic strings repeated (P2-4)            |
| Parallel-safe                                     | ✅ Pass                                      |

---

## Coverage Boundary Note

This review **does not** score coverage — that's the `trace` workflow's job. If you want a coverage map of new code from Stories 21-11/12/13 against acceptance criteria, run `/bmad-testarch-trace` next.

---

## Recommended Next Actions

1. **Ship the two P1 fixes this sprint** — `TimeProvider` injection is cheap and high-value; `InternalsVisibleTo` removes silent-rename risk.
2. **Batch the two 5-minute P2 fixes** (idempotency assertion + `.Result` removal) into the same PR.
3. **Schedule `EventStoreAggregateTests` decomposition** — pattern after the `AggregateActorTests` work that's already in flight.
4. **Run `/bmad-testarch-trace`** if you need coverage verification of recent UI bug-fix stories against their acceptance criteria.

---

## Workflow Metadata

- Next recommended workflow: `trace` (coverage mapping for 21-11/12/13) or `automate` (fill gaps once P1s are fixed).
- Artifacts location: `_bmad-output/test-artifacts/`
- No browser automation evidence collection needed (backend-heavy review; UI reflection evidence is source-level).
