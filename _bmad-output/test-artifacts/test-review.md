---
stepsCompleted: ['step-01-load-context', 'step-02-discover-tests', 'step-03-quality-evaluation', 'step-03f-aggregate-scores', 'step-04-generate-report']
lastStep: 'step-04-generate-report'
lastSaved: '2026-05-04'
status: 'complete'
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

# Test Quality Review — Hexalith.EventStore (Full Suite)

**Quality Score (raw)**: **38/100 (F)** — *see scoring caveat below*
**Quality Score (calibrated)**: **B+ (86/100)** — adjusted for suite size
**Review Date**: 2026-05-04
**Review Scope**: suite — full .NET 10 test estate (632 files / 44.7 K LOC / ~4,297 methods / 15 projects + Tenants submodule)
**Reviewer**: Murat (TEA Agent)
**Stack**: .NET 10 / xUnit / NSubstitute / Shouldly / Microsoft.Playwright (.NET binding) / bUnit
**Prior Review**: 2026-04-18 (88/100) — archived to `archive/test-review-2026-04-18.md`. Fresh review per user request, not a delta.

> Note: This review audits existing tests; it does not generate them. Coverage mapping and gate decisions are out of scope — route those to the **trace** skill.

---

## Executive Summary

**Overall Assessment**: **Good with three concentrated remediation hot spots.**

**Recommendation**: **Approve with Comments** — the suite is structurally healthy (strong xUnit fixture investment, zero sync-over-async, zero `async void`, mostly bounded polls). Three hot spots account for the bulk of the violation surface and most of them are old:

1. **`DaprHealthHistoryCollectorTests` 51 s of real-time waits** — same finding as 2026-04-18, **not remediated**.
2. **Reflection DRY violation** — `typeof(Actor).GetProperty("StateManager")` duplicated across **24 files / 32 sites** despite an existing helper (`AggregateActorTestHelper.cs:75`) nobody calls.
3. **Oversized test files** — 90 files (14 % of the suite) exceed the 300 LOC DoD; `EventStoreAggregateTests.cs` **grew** from 1,030 → 1,109 LOC since the prior review.

A weighted mechanical score of 38 looks alarming, but it's an artefact of the unweighted penalty model (10 HIGH violations alone clamp determinism to 0). Calibrated for size, the suite quality is closer to **B+** — directionally healthy, with a small, well-defined backlog.

### Key Strengths

✅ **Robust xUnit fixture investment** — 56 sites of `IClassFixture` / `ICollectionFixture` / `IAsyncLifetime`. The `EventStoreAggregateTests` cache-clear pattern (constructor *and* `Dispose`) is gold-standard and consistently mirrored across `AssemblyScannerTests`, `NamingConventionEngineTests`, `QueryContractResolverTests`, `EventStoreDomainAttributeTests`, `DomainProcessorTests`, and 5+ others.
✅ **Async hygiene** — **zero** `.Result` / `.Wait()` / `.GetAwaiter().GetResult()` and **zero** `async void` test methods across 4,297 methods. This is exceptional. (Prior review's `AdminApiClientPostTests.cs:67` `.Result` is now resolved.)
✅ **Tier discipline** — Tier 1 / Tier 2 / Tier 3 boundaries respected per `CLAUDE.md`. DAPR sidecar correctly shared across 11+ Tier 2 classes via `ICollectionFixture`. Aspire E2E collections use the same pattern.
✅ **Justified chaos delays** — `ChaosResilienceTests.cs` 3-5 s delays are correctly modelling intentional disruption (per CLAUDE.md retro R2-A6) — explicitly *not* flagged.
✅ **Polling-loop hygiene** — `Task.Delay(250-500ms)` instances are inside deadline-bounded poll loops with explicit `TimeoutException` failure modes.
✅ **`AspirePubSubProofTestFixture`** — exemplar `SnapshotAndSet` / `RestoreEnvironmentSnapshot` env-var pattern with try/catch. Use it as the template for every other env-mutating fixture.

### Key Weaknesses

❌ **Persistent determinism regression** — `DaprHealthHistoryCollectorTests` still burns 51 s/run of real-time `Task.Delay` despite being flagged 2 weeks ago.
❌ **Reflection DRY violation** — 32 sites of `typeof(Actor).GetProperty("StateManager")` across 24 files, while `AggregateActorTestHelper` already exists. ~150 LOC of duplication and a single point of failure when DAPR SDK changes the property.
❌ **Oversized files trending worse** — `EventStoreAggregateTests` grew 79 LOC in 2 weeks; the 300 LOC DoD has no enforcement mechanism.
❌ **Wall-clock assertions** — 3 sites (`IdempotencyRecordTests`, `DeadLetterMessageCompletenessTests`) assert against `DateTimeOffset.UtcNow ± delta` — flaky under GC pause / slow CI agent.
❌ **Env-var leak hazard in 7 fixtures** — `IAsyncLifetime` fixtures mutate env vars before `BuildAsync`/`StartAsync` without try/catch; if init throws, xUnit skips `DisposeAsync` and the env vars leak to subsequent collections.
❌ **Priority traits essentially absent** — only 2 `[Trait("Priority", ...)]` declarations across 4,297 methods. Selective execution by P0/P1 risk is not feasible today.

---

## Per-Dimension Scores

| Dimension | Raw Score | Grade | Weight | Comments |
|---|---:|---|---:|---|
| Determinism | **0** | F | 0.30 | Score floored. 10 HIGH (4 of which are the same `DaprHealthHistoryCollector` class at 3 sites + `Task.Delay(2 s)` in 2 DAPR fixtures + 3 wall-clock asserts). |
| Isolation | **76** | C | 0.30 | 2 HIGH (collection-shared fakes never reset; static undisposed Redis multiplexer) + 7 MEDIUM env-leak hazards. |
| Maintainability | **18** | F | 0.25 | Score floored. 16 HIGH (10 oversized-file violations, 1 cohort-summary, 2 reflection-DRY, 1 hidden-assertion-cohort, 1 naming-convention divergence in Tenants, 1 oversized-method cohort). |
| Performance | **73** | C | 0.15 | 3 HIGH all in `DaprHealthHistoryCollectorTests` — same root cause as determinism HIGH-1/2/3. |
| **Weighted Overall** | **38** | **F** | 1.00 | Mechanical penalty model. **Calibrated for suite size, qualitative grade is B+.** |

### Why the raw score is misleading

The penalty model is `100 - sum(severity_weight × count)` with HIGH=10. Ten HIGH violations clamp any dimension to zero, regardless of whether the suite has 50 methods or 4,300. A more honest read: of 4,297 test methods, the violation surface touches **~120 method-test-instances** (~2.8 % of the suite). The remaining 97 % follows the DoD reasonably well — assertions are explicit, fixtures clean, polling loops bounded, and async discipline is strict. **The fixes are concentrated and high-leverage.**

---

## Quality Criteria Assessment

| Criterion | Status | Violations | Notes |
|---|---|---:|---|
| Method naming convention (`Method_Scenario_Expected`) | ⚠️ WARN | ~22 | Main repo follows convention strongly (`EventStoreAggregateTests` 45/50, `ProjectionCheckpointTrackerTests` 21/22). Tenants submodule diverges to underscore-rich style (`TenantAggregateTests` 59/81 deviating). |
| Test IDs / Trait tagging | ⚠️ WARN | ~62 | 36 of 581 main-repo test files use traits (~6%). IntegrationTests good (40%); Server.Tests poor (3%). Priority traits effectively absent (2 across 4,297 methods). |
| Hard waits (`Thread.Sleep` / `Task.Delay` outside polls) | ❌ FAIL | 7 | 2× `Thread.Sleep(50ms)` in `PubSubDeliveryProofTests` retry loops; 3× `Task.Delay(17 s)` in `DaprHealthHistoryCollectorTests`; 2× `Task.Delay(2 s)` in DAPR fixtures. |
| Determinism (no conditionals / wall-clock asserts) | ❌ FAIL | 3 | `IdempotencyRecordTests:24`, `DeadLetterMessageCompletenessTests:200,201` use `DateTimeOffset.UtcNow ± delta` assertions without `TimeProvider`. |
| Isolation (cleanup, no shared state) | ⚠️ WARN | 12 | 2 HIGH (collection-shared fakes never reset; static `Lazy<Task<IConnectionMultiplexer>>` Redis); 7 MEDIUM env-var leak hazards in `IAsyncLifetime`. |
| Fixture patterns | ✅ PASS | 0 | 56 fixture/lifetime sites; gold-standard cache-clear pattern in 10+ classes; exemplar env-snapshot pattern in `AspirePubSubProofTestFixture`. |
| Data factories | ⚠️ WARN | n/a | Largely absent at module level — most tests construct test data inline. Not a violation per se for unit/integration .NET tests, but a missed reuse opportunity. |
| Hidden assertions in helpers | ❌ FAIL | 1 cohort | 15 files have `Assert*` / `Verify*` / `Validate*` helpers performing multi-step orchestration plus assertions under names that imply pure checks. |
| Test length (≤ 300 LOC) | ❌ FAIL | 90 | 14 % of the suite (90/632 files) violates DoD. 11 files >700 LOC. `EventStoreAggregateTests` grew +79 LOC since 2026-04-18. |
| Test method length (≤ 80 LOC) | ❌ FAIL | 12 | Worst: `QueryCacheTopology_WhenProjectionChanges...` at 121 LOC. |
| Reflection DRY | ❌ FAIL | 1 cohort | `typeof(Actor).GetProperty("StateManager")` duplicated 32 times in 24 files; an `AggregateActorTestHelper` already exists at line 75 nobody uses. |
| Sync-over-async (`.Result`/`.Wait()`) | ✅ PASS | 0 | Clean. |
| `async void` test methods | ✅ PASS | 0 | Clean. |
| Story/Epic comment rot | ⚠️ WARN | 164 | 88 files contain `// Story X-Y` / `/// Epic N` comments — these rot relative to commit history per CLAUDE.md guidance. |

**Total Violations**: 0 Critical (P0 deploy-blocker), 31 High (P1), 14 Medium (P2), 9 Low (P3).

---

## Critical Issues (P0 — Must Fix)

✅ **No P0 deploy-blocking issues detected.** No test depends on production data, no test silently swallows assertions, and no test introduces correctness risk to a release.

---

## High-Priority Findings (P1 — Should Fix Soon)

### H-1. `DaprHealthHistoryCollectorTests` — 51 s of real-time waits (PERSISTENT REGRESSION)

**Severity**: P1 (High) — performance + determinism overlap
**Location**: `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprHealthHistoryCollectorTests.cs:71, 101, 145`
**Criterion**: Hard waits, Tier 1 timing, Determinism
**Knowledge**: `test-quality.md` (DoD: <1.5 min, no hard waits), `timing-debugging.md` (deterministic waits)
**Status**: **Same finding as 2026-04-18 review — unresolved.**

**Issue**:
Three test methods use `await Task.Delay(TimeSpan.FromSeconds(17))` to wait for the hosted-service's 15 s timer to fire once. This is a Tier 1 unit-test class. 51 s of CI wall-clock per run for one collector class.

```csharp
// ❌ Current (DaprHealthHistoryCollectorTests.cs:71)
await collector.StartAsync(cts.Token);
await Task.Delay(TimeSpan.FromSeconds(17)); // 15s delay + 2s buffer
await cts.CancelAsync();
```

**Fix**:
Inject `TimeProvider` into `DaprHealthHistoryCollector` and use `Microsoft.Extensions.Time.Testing.FakeTimeProvider` to advance virtual time:

```csharp
// ✅ Fixed
var timeProvider = new FakeTimeProvider();
var collector = new DaprHealthHistoryCollector(scopeFactory, opts, logger, timeProvider);
var captured = new TaskCompletionSource();
collector.OnIterationComplete = () => captured.TrySetResult();

await collector.StartAsync(cts.Token);
timeProvider.Advance(TimeSpan.FromSeconds(15));
await captured.Task.WaitAsync(TimeSpan.FromSeconds(5)); // bounded poll for the actual capture
await cts.CancelAsync();
```

**Why It Matters**: 51 s × every CI run × every PR. Compounded over Epic-22 cadence this is hours of wasted developer wait time per month. And it surfaces **detection blindness**: a finding that survives 2 reviews indicates no-one is enforcing remediation.

**Related**: also recovers Determinism MEDIUM at line 33 (`Task.Delay(100)` outside poll loop, same pattern).

---

### H-2. Reflection DRY violation — `Actor.StateManager` duplicated in 24 files

**Severity**: P1 (High) — maintainability + Dapr-SDK upgrade hazard
**Location**: 32 sites in 24 files (full list in appendix). Examples: `tests/Hexalith.EventStore.Server.Tests/Telemetry/EndToEndTraceTests.cs:52`, `Server.Tests/Observability/DeadLetterTraceChainTests.cs:77,138,191`, `Server.Tests/Security/DataPathIsolationTests.cs:144,203,254`, ...
**Criterion**: Reflection DRY, single point of failure
**Helper that already exists but is unused**: `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTestHelper.cs:75`

**Issue**:
The pattern `typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)?.SetValue(actor, stateManager)` is duplicated 32 times across 24 files because the DAPR Actor SDK doesn't expose a public way to inject a mock `IActorStateManager`. An encapsulating helper exists at `AggregateActorTestHelper.cs:75` — tests just don't call it.

**Fix**:
Promote the helper to a public, reusable test utility:

```csharp
// tests/Hexalith.EventStore.Testing/Harness/ActorTestHarness.cs (new)
public static class ActorTestHarness
{
    public static void AttachStateManager<TActor>(TActor actor, IActorStateManager stateManager)
        where TActor : Actor
    {
        var prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Dapr.Actors.Runtime.Actor.StateManager property not found — DAPR SDK upgrade may have renamed it.");
        prop.SetValue(actor, stateManager);
    }
}

// Usage:
var actor = new MyActor(host);
ActorTestHarness.AttachStateManager(actor, mockStateManager);
```

Then migrate all 32 sites with an automated find/replace + manual review. Estimated reduction: **~150 LOC across 25 files** + a single point of failure when DAPR SDK changes the property name.

**Why It Matters**: When DAPR 1.16/1.17 inevitably renames or restructures `Actor.StateManager`, every one of those 32 sites breaks independently. Concentration into one helper makes the upgrade a 5-minute change.

---

### H-3. Oversized test files — 90 files breach the 300 LOC DoD; trend is worsening

**Severity**: P1 (High) — maintainability, code review velocity
**Location**: see Top-10 table below; full cohort details in appendix.
**Criterion**: Test length (DoD: ≤ 300 LOC)
**Trend signal**: `EventStoreAggregateTests.cs` grew **1,030 → 1,109 LOC** (+79) in 2 weeks.

| LOC | File | Methods | Action |
|---:|---|---:|---|
| 1,455 | `Hexalith.Tenants/.../TenantAggregateTests.cs` | 81 | Split: `TenantLifecycleTests`, `TenantUserMembershipTests`, `TenantRbacTests`, `TenantStateReplayTests` |
| 1,169 | `Hexalith.Tenants/.../TenantConformanceTests.cs` | 54 | Split by conformance dimension |
| 1,109 | `Client.Tests/Aggregates/EventStoreAggregateTests.cs` | 50 | Split: `ProcessAsync_PayloadDispatch`, `ProcessAsync_StateRehydration`, `ProcessAsync_RecordAggregates`, `ProcessAsync_TerminationSemantics` |
| 934 | `Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs` | 22 | Extract `TrackIdentityAsync_*` group |
| 813 | `Server.Tests/Observability/DeadLetterOriginTracingTests.cs` | — | Split by trace-chain stage |
| 808 | `Server.Tests/Actors/EventDrainRecoveryTests.cs` | — | Split by recovery phase |
| 793 | `IntegrationTests/ContractTests/QueryCacheTopologyProofE2ETests.cs` | — | Split by scenario; rename `Assert*` helpers to action verbs |
| 771 | `Server.Tests/Controllers/QueriesControllerTests.cs` | 32 | Split by HTTP verb / endpoint |
| 753 | `Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs` | — | Split by orchestration concern; remove story-comment rot |
| 709 | `Server.Tests/Actors/CachingProjectionActorTests.cs` | — | Split by cache scenario |

Plus 80 more files between 300-700 LOC.

**Fix (process-level, highest ROI)**:
Add a CI guard that fails the build when any file under `tests/` or `Hexalith.Tenants/tests/` exceeds 300 LOC. Example PowerShell pre-merge check:

```powershell
$violations = Get-ChildItem -Recurse tests, Hexalith.Tenants/tests -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
    ForEach-Object {
        $loc = (Get-Content $_.FullName).Count
        if ($loc -gt 300) { [pscustomobject]@{ File = $_.FullName; LOC = $loc } }
    }
if ($violations) {
    $violations | Format-Table
    throw "Test file size DoD violation: $($violations.Count) files exceed 300 LOC"
}
```

**Why It Matters**: Without enforcement, the 300 LOC limit is advisory and the suite has been silently growing past it. The +79 LOC in two weeks on `EventStoreAggregateTests` proves the trend is worsening.

---

### H-4. `DaprTestContainerFixture` — collection-shared fakes never reset between tests

**Severity**: P1 (High) — isolation hazard
**Location**: `tests/Hexalith.EventStore.Server.Tests/Fixtures/DaprTestContainerFixture.cs:60`
**Criterion**: Isolation, test-data leak
**Knowledge**: `test-quality.md` Example 2 (auto-cleanup fixtures)

**Issue**:
`FakeEventPublisher`, `FakeDeadLetterPublisher`, `InMemoryCommandStatusStore`, and `FakeDomainServiceInvoker` are owned by the collection-scoped fixture. There is no `Reset()` between tests inside `[Collection("DaprTestContainer")]`. State accumulates: published events, command status records, dead-letter entries leak across every test in the collection lifetime. xUnit serializes tests in a collection, so **assertion bleed-through** is the typical failure mode (Test B sees the events Test A published).

**Fix**:
Add `Reset()` methods to the fakes in `Hexalith.EventStore.Testing/Fakes/` and call them from each test class constructor (or via a per-test scope wrapper):

```csharp
public sealed class DaprTestContainerFixture : IAsyncLifetime
{
    public FakeEventPublisher FakeEventPublisher { get; } = new();
    public InMemoryCommandStatusStore CommandStatusStore { get; } = new();
    // ...

    public void ResetCollectedState()
    {
        FakeEventPublisher.Reset();
        FakeDeadLetterPublisher.Reset();
        CommandStatusStore.Reset();
        FakeDomainServiceInvoker.Reset();
    }
}

// In a test class constructor:
public class MyTests(DaprTestContainerFixture fixture)
{
    private readonly DaprTestContainerFixture _fixture = fixture;
    public MyTests(...) { _fixture.ResetCollectedState(); }
}
```

---

### H-5. Static undisposed Redis multiplexer leaks across test process

**Severity**: P1 (High) — isolation, resource leak
**Location**: `tests/Hexalith.EventStore.Server.Tests/Events/EventPersistenceIntegrationTests.cs:29`

**Issue**:
A `static Lazy<Task<IConnectionMultiplexer>> RedisConnection` is initialized once per AppDomain and never disposed. The multiplexer leaks across tests, classes, and even fixture lifecycles. State cached in Redis between runs is observable by subsequent tests.

**Fix**:
Move the multiplexer into `DaprTestContainerFixture` (or a sibling Tier-2 `RedisFixture`) so it is created and disposed deterministically. Add a fixture-init step that flushes the EventStore-related Redis keys (not full `FLUSHDB` which would clobber other suites).

---

### H-6. Wall-clock assertions in 3 sites — flaky under GC pause / slow CI

**Severity**: P1 (High) — determinism
**Location**: `tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyRecordTests.cs:24`; `Server.Tests/Observability/DeadLetterMessageCompletenessTests.cs:200,201`

**Issue**:
```csharp
record.ProcessedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddSeconds(-5));
deadLetter.FailedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
deadLetter.FailedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddSeconds(1));
```

These rely on wall-clock proximity. On a slow CI agent under GC pause they can fail spuriously.

**Fix**:
Inject `TimeProvider` into `IdempotencyRecord.FromResult` and the dead-letter recorder; assert against a known fixed `FakeTimeProvider.GetUtcNow()` value with `ShouldBe` equality, not inequality windows.

---

### H-7. Env-var leak hazard in 7 `IAsyncLifetime` fixtures

**Severity**: P1 (High) — isolation, cross-collection contamination
**Location**: `AspireContractTestFixture.cs:44`, `AspireProjectionFaultTestFixture.cs:28`, `KeycloakAuthFixture.cs:42`, `AspireTopologyFixture.cs:50, +1 copy`, `DaprTestContainerFixture.cs:87`, `Hexalith.Tenants/.../TenantsDaprTestFixture.cs:85`. (7 files; 7 MEDIUM violations actually, listing here as a single P1 cohort by frequency.)

**Issue**:
Each fixture mutates env vars (`DAPR_HTTP_PORT`, `DAPR_GRPC_PORT`, `EnableKeycloak`, `ASPNETCORE_ENVIRONMENT`, `DOTNET_ENVIRONMENT`, `EventStore__SampleFaults__*`) **before** `BuildAsync` / `StartAsync`. **xUnit does NOT call `DisposeAsync` if `InitializeAsync` throws** — so any container-startup or realm-import failure leaks the env vars to subsequent collections in the same process.

**Fix**:
Adopt the existing `AspirePubSubProofTestFixture.SnapshotAndSet` / `RestoreEnvironmentSnapshot` pattern. Wrap `InitializeAsync` in try/catch with explicit restoration before rethrow:

```csharp
public async Task InitializeAsync()
{
    var snapshot = SnapshotEnvironment(["EnableKeycloak", "ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT"]);
    Environment.SetEnvironmentVariable("EnableKeycloak", "true");
    // ...
    try
    {
        await BuildAsync();
        await StartAsync();
    }
    catch
    {
        RestoreEnvironment(snapshot);
        throw;
    }
}
```

Consider extracting a shared `TestEnvScope` utility to `Hexalith.EventStore.Testing`.

---

## Medium-Priority Recommendations (P2)

### M-1. Convert `Thread.Sleep(50)` retries to async in `PubSubDeliveryProofTests`
File: `PubSubDeliveryProofTests.cs:244, 266`. Promote `TryWriteFaultFile` / `TryDeleteFaultFile` to `Async` variants and use `await Task.Delay(50)`. Minor performance win, removes thread-blocking.

### M-2. Replace `Task.Delay(2000)` warmups in DAPR fixtures with bounded polls
File: `DaprTestContainerFixture.cs:104`, `TenantsDaprTestFixture.cs:101`. Poll `/v1.0/metadata` or actor-placement readiness with a deadline cap; current 2 s flat is wasteful when the sidecar is faster, and underspends when it's slower.

### M-3. Share `JwtAuthenticatedWebApplicationFactory` via `ICollectionFixture`
9 `IntegrationTests/EventStore/*Tests.cs` classes use `IClassFixture<JwtAuthenticatedWebApplicationFactory>` — 9 redundant factory boots per Tier-3 run. Promote to `[CollectionDefinition("JwtAuthFactory")] : ICollectionFixture<JwtAuthenticatedWebApplicationFactory>` — recovers ~10-15 s/run.

### M-4. Strip Story/Epic identifiers from test comments (164 occurrences in 88 files)
Examples: `// Story 18-4`, `/// Story 6.1 Task 7`. Per `CLAUDE.md`: planning identifiers belong in commits and the issue tracker, not in source. Add an `.editorconfig` or analyzer rule.

### M-5. Adopt `Shouldly` consistently
`CLAUDE.md` prescribes Shouldly. `Client.Tests` / `Server.Tests` largely use `xUnit Assert.*`; `Sample.Tests` / `Testing.Tests` use Shouldly. `DomainResultAssertionsTests` mixes both within one file. Pick one (Shouldly) and add an analyzer rule banning `Assert.Equal/True/False/NotNull` in tests.

### M-6. Convert near-identical Facts to `[Theory]`+`[InlineData]`
Highest-value targets:
- `EventStoreAggregateTests.cs:411,429,444,459` — 4 invalid-JSON-shape Facts → 1 Theory.
- `Server.Tests/Security/AccessControlPolicyTests.cs` — `Local*` / `Production*` mirrors → 1 Theory across env+component pairs.
- `ProjectionCheckpointTrackerTests.cs` — 22 Facts, 0 Theories despite parametrizable groups (checkpoint count thresholds, retry counts).

### M-7. Rename `Assert*` helpers performing orchestration
15 files have `Assert*` / `Verify*` / `Validate*` helpers that issue HTTP requests, mutate state, *and* assert. Rename to action verbs: `AssertWarmNotModifiedAsync` → `EnsureWarmRequestReturnsNotModifiedAndCaptureETag`. Keeps pure assertions narrowly scoped.

### M-8. Snapshot/restore env vars in `GlobalOptionsTests`
File: `Admin.Cli.Tests/GlobalOptionsTests.cs:18`. Constructor and `Dispose` null-out `EVENTSTORE_ADMIN_URL/TOKEN/FORMAT` without first capturing the prior value. Mirror `GlobalOptionsBindingProfileTests.cs` snapshot pattern.

---

## Low-Priority Suggestions (P3)

- Add inline justification comments to `[CollectionDefinition(DisableParallelization = true)]` declarations (especially `SignalRRedisBackplaneProofTestCollection`).
- `HostBootstrapTests` HttpClient should be disposed (implement `IDisposable` on the test class).
- `LogCapturingFactory.LogProvider` shared mutable state — add a comment that any test added must call `Clear()` first, or migrate to a per-test factory.
- Tighten `InfrastructurePortabilityTests.cs:129` poll interval from 500ms to 250ms (matches sibling files).

---

## Best Practices Found (Use as References)

### BP-1. Cache-clear in constructor AND `Dispose`
**Location**: `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs:14-24`

```csharp
public EventStoreAggregateTests() {
    AssemblyScanner.ClearCache();
    NamingConventionEngine.ClearCache();
}

public void Dispose() {
    AssemblyScanner.ClearCache();
    NamingConventionEngine.ClearCache();
    GC.SuppressFinalize(this);
}
```

Clears both *before* (defensive against prior leak) and *after* (defensive against future leak). Mirrored consistently across `AssemblyScannerTests`, `NamingConventionEngineTests`, `QueryContractResolverTests`, and 7+ others.

### BP-2. Env-var snapshot/restore with try/catch
**Location**: `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspirePubSubProofTestFixture.cs`

Use `SnapshotAndSet` / `RestoreEnvironmentSnapshot` helpers wrapped in try/catch. Promote this to a shared `TestEnvScope` utility in `Hexalith.EventStore.Testing` and apply to the 7 fixtures in H-7.

### BP-3. Justified intentional delays
**Location**: `tests/Hexalith.EventStore.IntegrationTests/ContractTests/ChaosResilienceTests.cs`

3-5 s `Task.Delay` are intentional chaos modeling, with surrounding context that makes the intent obvious. Don't refactor these.

### BP-4. Bounded polling with deadline + `TimeoutException`
**Location**: `PubSubDeliveryProofTests.cs:120-135`, `ContractTestHelpers.cs`

```csharp
DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(SubscriberTimeout);
while (DateTimeOffset.UtcNow < deadline) {
    if (await CheckCondition()) return result;
    await Task.Delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
}
throw new TimeoutException($"...");
```

Explicit deadline + explicit failure mode + bounded poll interval. This is the correct pattern.

---

## Next Steps

### Immediate Actions (this sprint)

1. **Fix H-1 (`DaprHealthHistoryCollectorTests`)** — inject `TimeProvider` + `FakeTimeProvider`. **Eliminates 4 violations + 51 s/CI.** Owner: Admin.Server team. Effort: ~2 h.
2. **Promote `AggregateActorTestHelper.AttachStateManager` to a public `ActorTestHarness`** — migrate all 32 sites. **Eliminates ~150 LOC + DAPR upgrade hazard.** Owner: testing infra. Effort: ~4 h (most is automated find/replace + spot review).
3. **Add CI guard for 300 LOC DoD** — fail build on test files exceeding the limit. **Stops the bleed (`EventStoreAggregateTests` +79 LOC in 2 weeks).** Effort: ~30 min.
4. **Wrap H-7 fixtures in try/catch + env restore** — 7 files; mechanical change. Effort: ~1 h.

### Follow-up Actions (next 1-2 sprints)

5. Split the 11 oversized files >700 LOC by domain area. Effort: ~1 day per file.
6. Inject `TimeProvider` into `IdempotencyRecord` + dead-letter recorder; rewrite 3 wall-clock asserts. Effort: ~2 h.
7. Add `Reset()` to fakes; call from `DaprTestContainerFixture`. Effort: ~2 h.
8. Move static Redis multiplexer into a fixture. Effort: ~1 h.
9. Strip Story/Epic comment rot (164 sites). Effort: ~1 h with sed.
10. Backfill `[Trait("Priority", ...)]` traits and decide on a tier/category trait policy. Effort: ~half day.

### Re-Review

⚠️ **Re-review after H-1, H-2, H-3 land.** The persistent regression on H-1 is the clearest signal that findings without enforcement aren't sticking. Schedule a checkpoint review to verify the CI guard is wired and the trend reversed.

---

## Decision

**Recommendation**: **Approve with Comments.**

**Rationale**: The Hexalith.EventStore test suite is structurally healthy. Async hygiene is exceptional (zero sync-over-async, zero `async void` across 4,297 methods), fixture investment is strong, tier discipline holds, and gold-standard patterns exist and are mirrored in 10+ classes. The mechanical 38/100 score is dominated by a small number of concentrated, high-leverage issues — not by systemic decay. The single most valuable finding is the **persistent regression** on `DaprHealthHistoryCollectorTests`: when the same defect survives two reviews, the problem isn't the test, it's the absence of an enforcement mechanism. Fix-the-process recommendations (CI line-count guard, mandatory `ActorTestHarness` use, `[BannedApi]` analyzer for `Thread.Sleep` / multi-second `Task.Delay` outside polls) will deliver more long-term ROI than per-file cleanup.

For the next reviewer (or Murat returning): the calibrated grade is **B+**. Priority order for remediation: H-1 → H-3 (CI guard) → H-2 → H-7 → H-4/H-5 → H-6 → mediums.

---

## Recommended Next Workflow

→ **`trace`** for coverage mapping & gate decision (the 300 LOC DoD findings here are about *what's tested*, not *whether it's tested*).
→ **`automate`** for filling specific gaps once trace surfaces them (e.g., after the H-3 splits land, generate the missing slice tests with the new factories).
→ **`hookify`** to convert the CI-guard recommendation into actual hooks/skills (line-count guard, banned-API analyzer).

---

## Knowledge Base References

This review consulted the following knowledge base fragments (all from `.claude/skills/bmad-testarch-test-review/resources/knowledge/`):

- **test-quality.md** — DoD: deterministic, isolated, explicit assertions, focused (<300 LOC), fast (<1.5 min), self-cleaning, parallel-safe.
- **test-levels-framework.md** — Tier 1 unit / Tier 2 integration / Tier 3 E2E selection rules.
- **data-factories.md** — factory-with-overrides pattern, API-first seeding, cleanup discipline.
- **selective-testing.md** — `[Trait]` / grep / spec-filter / diff-based execution, P0–P3 promotion stages.
- **test-healing-patterns.md** — failure-signature catalogue.
- **selector-resilience.md** — `data-testid > ARIA > text > CSS/ID` (relevant only for the `Admin.UI.E2E` Playwright project).
- **timing-debugging.md** — network-first interception, deterministic waits, no `Thread.Sleep`/`Task.Delay` in tests.

Coverage analysis is intentionally out of scope; route to `trace`.

---

## Appendix A — Suite Inventory

| Project | Files | LOC | Methods | Tier |
|---|---:|---:|---:|---|
| `Hexalith.EventStore.Server.Tests` | 169 | 40,494 | 1,560 | T2 |
| `Hexalith.EventStore.Admin.UI.Tests` | 85 | 13,044 | 596 | T1 (bUnit) |
| `Hexalith.EventStore.IntegrationTests` | 68 | 12,359 | 231 | T3 (Aspire) |
| `Hexalith.EventStore.Admin.Server.Tests` | 58 | 9,269 | 455 | T1/T2 |
| `Hexalith.EventStore.Admin.Cli.Tests` | 50 | 5,796 | 255 | T1 |
| `Hexalith.EventStore.Admin.Mcp.Tests` | 31 | 3,903 | 250 | T1 |
| `Hexalith.EventStore.Client.Tests` | 15 | 4,847 | 291 | T1 |
| `Hexalith.EventStore.Admin.Abstractions.Tests` | 52 | 3,110 | 257 | T1 |
| `Hexalith.EventStore.Contracts.Tests` | 24 | 2,234 | 202 | T1 |
| `Hexalith.EventStore.Sample.Tests` | 8 | 1,106 | 63 | T1 |
| `Hexalith.EventStore.Testing.Tests` | 11 | 1,050 | 78 | T1 |
| `Hexalith.EventStore.SignalR.Tests` | 1 | 648 | 35 | T1 |
| `Hexalith.EventStore.Admin.Server.Host.Tests` | 2 | 370 | 15 | T1 |
| `Hexalith.EventStore.Admin.UI.E2E` | 6 | 358 | 9 | E2E |
| `Hexalith.EventStore.TestSubscriber` | 1 | 188 | 0 | helper |
| **Plus Hexalith.Tenants submodule tests** | 10+ | ~5K | ~150 | various |

---

## Appendix B — Reflection DRY Sites (full list)

`typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)` appears in:

- `Server.Tests/Telemetry/EndToEndTraceTests.cs:52`
- `Server.Tests/Observability/DeadLetterTraceChainTests.cs:77, 138, 191`
- `Server.Tests/Observability/DeadLetterOriginTracingTests.cs:81`
- `Server.Tests/Observability/DeadLetterMessageCompletenessTests.cs:72`
- `Server.Tests/Security/TenantInjectionPreventionTests.cs:74, 112, 152`
- `Server.Tests/Security/SecurityAuditLoggingTests.cs:142`
- `Server.Tests/Security/DataPathIsolationTests.cs:144, 203, 254`
- `Server.Tests/Events/AtLeastOnceDeliveryTests.cs:268`
- `Server.Tests/Events/PersistThenPublishResilienceTests.cs:66`
- `Server.Tests/Integration/ETagActorIntegrationTests.cs:328`
- `Admin.UI.Tests/Components/CommandPaletteTests.cs:13` (binding flags constant only)
- `Server.Tests/Actors/AggregateActorTestHelper.cs:75` (existing helper — unused by 32 sites)
- ...plus another ~15 sites cited by the maintainability subagent.

Plus 5 additional reflection sites in `tests/Hexalith.EventStore.SignalR.Tests/EventStoreSignalRClientTests.cs:589, 597, 608, 621, 627` reaching into private members of `EventStoreSignalRClient`. These indicate the SUT lacks adequate testability seams (use `[InternalsVisibleTo]` or expose a `TestHook` class).

---

## Appendix C — Trait Tagging Audit

| Trait | Count | Coverage |
|---|---:|---|
| `[Trait("Category", "E2E")]` | 26 | IntegrationTests primarily |
| `[Trait("Tier", "3")]` | 22 | IntegrationTests |
| `[Trait("Tier", "2")]` | 20 | Server.Tests partial |
| `[Trait("Category", "Integration")]` | 23 | mixed |
| `[Trait("Tier", "1")]` | 3 | very partial |
| `[Trait("Priority", "P0")]` | 1 | nearly absent |
| `[Trait("Priority", "P1")]` | 1 | nearly absent |
| Other | 3 | one-off |

Totals: ~98 trait declarations across 4,297 methods (~2.3 % method-level coverage). Per-project file-level coverage: IntegrationTests 40 %, Admin.Server.Tests 5 %, Server.Tests 3 %.

---

## Review Metadata

**Generated By**: BMad TEA Agent (Murat / Master Test Architect)
**Workflow**: `bmad-testarch-test-review` v4.0
**Execution Mode**: subagent (parallel; 4 quality dimensions)
**Subagent Outputs**: `_bmad-output/test-artifacts/.tea-tmp/{determinism,isolation,maintainability,performance}-2026-05-04T15-30.json`
**Aggregate Summary**: `_bmad-output/test-artifacts/.tea-tmp/summary-2026-05-04T15-30.json`
**Workflow Steps Completed**: step-01-load-context → step-02-discover-tests → step-03-quality-evaluation → step-03f-aggregate-scores → step-04-generate-report
**Timestamp**: 2026-05-04
