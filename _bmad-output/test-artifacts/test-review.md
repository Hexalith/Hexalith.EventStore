---
stepsCompleted: ['step-01-load-context', 'step-02-discover-tests', 'step-03-quality-evaluation', 'step-03f-aggregate-scores', 'step-04-generate-report']
lastStep: 'step-04-generate-report'
lastSaved: '2026-05-20'
status: 'complete'
humanApproval:
  status: 'approved'
  approvedBy: 'Jerome'
  approvedOn: '2026-05-20'
workflowType: 'testarch-test-review'
reviewScope: 'suite'
priorReview: 'archive/test-review-2026-05-04.md'
inputDocuments:
  - '_bmad-output/project-context.md'
  - '_bmad/tea/config.yaml'
  - '.agents/skills/bmad-testarch-test-review/resources/tea-index.csv'
  - '.agents/skills/bmad-testarch-test-review/resources/knowledge/test-quality.md'
  - '.agents/skills/bmad-testarch-test-review/resources/knowledge/data-factories.md'
  - '.agents/skills/bmad-testarch-test-review/resources/knowledge/test-levels-framework.md'
  - '.agents/skills/bmad-testarch-test-review/resources/knowledge/selective-testing.md'
  - '.agents/skills/bmad-testarch-test-review/resources/knowledge/test-healing-patterns.md'
  - '.agents/skills/bmad-testarch-test-review/resources/knowledge/selector-resilience.md'
  - '.agents/skills/bmad-testarch-test-review/resources/knowledge/timing-debugging.md'
  - '.agents/skills/bmad-testarch-test-review/resources/knowledge/overview.md'
  - '.agents/skills/bmad-testarch-test-review/resources/knowledge/api-request.md'
  - '.agents/skills/bmad-testarch-test-review/resources/knowledge/auth-session.md'
  - '.agents/skills/bmad-testarch-test-review/resources/knowledge/recurse.md'
  - '.agents/skills/bmad-testarch-test-review/resources/knowledge/playwright-cli.md'
  - '.agents/skills/bmad-testarch-test-review/resources/knowledge/pact-mcp.md'
  - 'https://learn.microsoft.com/en-us/dotnet/core/extensions/timeprovider-testing'
  - 'https://xunit.net/docs/shared-context'
  - 'https://playwright.dev/dotnet/docs/locators'
  - 'https://bunit.dev/docs/getting-started/writing-tests.html'
---

# Test Quality Review - Hexalith.EventStore

**Quality Score**: 69/100 (C - Needs Improvement)
**Calibrated Read**: B- for a large legacy suite, because the highest-value regression from the prior review is fixed.
**Human Approval**: Approved by Jerome on 2026-05-20.
**Review Date**: 2026-05-20
**Review Scope**: suite
**Reviewer**: Murat / TEA Agent
**Stack**: .NET 10, xUnit v3, Shouldly, NSubstitute, bUnit, Microsoft.Playwright .NET, Aspire/Dapr integration tests

Note: This review audits existing tests; it does not generate tests. Coverage mapping and gate decisions are out of scope here. Use `trace` for coverage decisions.

## Executive Summary

**Overall Assessment**: Needs Improvement, with a positive trend on the biggest prior flake/performance issue.

**Recommendation**: Approve with Comments.

The May 4 blocker-sized hot spot in `DaprHealthHistoryCollectorTests` was fixed properly: the collector now accepts `TimeProvider`, and the tests drive it with `FakeTimeProvider`. That is exactly the right direction and it removes the prior 51 seconds of wall-clock waiting.

The current risk has shifted. Maintainability is now the weak dimension: 113 test/support files exceed the 300 line DoD, the largest file is 1900 lines, and `Actor.StateManager` reflection injection is still duplicated across 35 sites. Isolation also needs attention because several collection-scoped fixtures mutate environment variables or shared fakes without a failure-safe reset story.

### Key Strengths

- `DaprHealthHistoryCollectorTests` now uses `FakeTimeProvider`; this resolves the prior review's largest performance/determinism finding.
- xUnit fixture investment is substantial: collection fixtures are used for Dapr, Aspire, Keycloak, Playwright, and pub/sub proof suites.
- bUnit is used for most Admin UI component behavior, keeping many UI checks below browser/E2E level.
- Integration tests have meaningful tenant/security coverage around Keycloak, Dapr access control, command lifecycle, and rate limiting.
- The suite keeps coverage concerns separated from review concerns; no coverage score is inferred here.

### Key Weaknesses

- 113 files are over 300 lines; top files are now 1900, 1178, 1052, and 1035 lines.
- Actor test harness reflection is repeated at 35 call sites instead of centralized.
- Only 2 `Priority` traits were found across roughly 5288 Fact/Theory declarations.
- Shared Dapr fakes and a static Redis multiplexer still have cross-test contamination risk.
- Browser E2E selectors lean heavily on CSS selectors instead of Playwright role locators or stable test IDs.

## Dimension Scores

| Dimension | Score | Grade | Notes |
|---|---:|---|---|
| Determinism | 78 | C | Major prior wall-clock wait fixed; remaining issues are wall-clock timestamp assertions and three small hard waits. |
| Isolation | 70 | C | Fixture model is good, but shared mutable fakes, static Redis lifetime, and env-var restore-on-init-failure hazards remain. |
| Maintainability | 52 | F | Oversized files and duplicated actor reflection dominate the risk. |
| Performance | 78 | C | Better than May 4; remaining costs are fixed Dapr warmup and repeated WebApplicationFactory startup. |
| Overall | 69 | C | Weighted: determinism 30%, isolation 30%, maintainability 25%, performance 15%. |

## Quality Criteria Assessment

| Criterion | Status | Violations | Notes |
|---|---|---:|---|
| Test discovery | PASS | 0 | 696 test/support files reviewed under `tests`, `samples`, and `perf`; submodules were not recursively initialized. |
| Hard waits | WARN | 4 | `DaprHealthHistoryCollectorTests` was fixed; remaining hard waits are short but still avoidable. |
| Wall-clock determinism | FAIL | 2 cohorts | `IdempotencyRecordTests` and `DeadLetterMessageCompletenessTests` assert against `DateTimeOffset.UtcNow` tolerance windows. |
| Isolation cleanup | FAIL | 3 cohorts | Shared Dapr fakes, static Redis connection, and env-var fixture startup failure paths. |
| Fixture patterns | PASS | 0 | Strong use of `IClassFixture`, `ICollectionFixture`, and `IAsyncLifetime`. |
| Test length | FAIL | 113 | 113 files over 300 lines; 10 files exceed 790 lines. |
| Priority markers | WARN | 1 cohort | Only 2 priority traits across roughly 5288 Fact/Theory declarations. |
| Selector resilience | WARN | 1 cohort | Admin UI E2E uses CSS selectors for navigation and Fluent UI shell elements. |
| Assertion style | WARN | 1 cohort | Shouldly is preferred locally, but many older tests still use xUnit `Assert.*`. |
| Performance | WARN | 3 | Fixed warmup and repeated factory startup are the main speed risks. |

**Total Violations**: 0 Critical, 6 High, 13 Medium, 1 Low.

## Critical Issues

No P0 deploy-blocking test quality issues detected.

## High-Priority Findings

### H-1. Oversized Test Files Are Growing Past The DoD

**Severity**: P1 (High)
**Location**: `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs:1`
**Criterion**: Maintainability / test file length
**Knowledge Base**: `test-quality.md`

The suite now has 113 test/support files over the 300 line DoD. The largest current files are:

| Lines | File |
|---:|---|
| 1900 | `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs` |
| 1178 | `tests/Hexalith.EventStore.Client.Tests/Aggregates/EventStoreAggregateTests.cs` |
| 1052 | `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprInfrastructureQueryServiceTests.cs` |
| 1035 | `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs` |
| 976 | `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs` |
| 947 | `tests/Hexalith.EventStore.Server.Tests/Actors/CachingProjectionActorTests.cs` |
| 928 | `tests/Hexalith.EventStore.Server.Tests/Controllers/QueriesControllerTests.cs` |
| 827 | `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DeadLettersPageTests.cs` |
| 813 | `tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterOriginTracingTests.cs` |
| 793 | `tests/Hexalith.EventStore.IntegrationTests/ContractTests/QueryCacheTopologyProofE2ETests.cs` |

**Recommendation**: add a CI/reporting guard for new or changed test files over 300 lines, then split the top files by behavior slice. Start with `ProjectionUpdateOrchestratorTests` because it is now the clear outlier.

### H-2. Actor StateManager Reflection Is Still Duplicated

**Severity**: P1 (High)
**Location**: `tests/Hexalith.EventStore.Server.Tests/Telemetry/EndToEndTraceTests.cs:52`
**Criterion**: Maintainability / SDK upgrade risk
**Knowledge Base**: `test-quality.md`

`typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)` appears in 35 sites. `AggregateActorTestHelper` already contains this pattern at `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTestHelper.cs:76`, but the helper is not broadly reused.

**Recommendation**: promote a reusable `ActorTestHarness.AttachStateManager(Actor actor, IActorStateManager stateManager)` into `src/Hexalith.EventStore.Testing` or a shared server test harness namespace. All direct reflection sites should call that helper so a future Dapr SDK change breaks in one place, not 35.

### H-3. Collection-Scoped Dapr Fakes Can Bleed State

**Severity**: P1 (High)
**Location**: `tests/Hexalith.EventStore.Server.Tests/Fixtures/DaprTestContainerFixture.cs:60`
**Criterion**: Isolation
**Knowledge Base**: `test-quality.md`, `data-factories.md`

`DaprTestContainerFixture` exposes collection-shared `FakeDomainServiceInvoker`, `FakeEventPublisher`, `FakeDeadLetterPublisher`, and `InMemoryCommandStatusStore`. Eight test classes share the collection. There is no fixture-level `ResetCollectedState()` call before each class or test.

**Recommendation**: add reset/clear methods on the fakes and call a fixture-level reset from each `[Collection("DaprTestContainer")]` test class constructor. This is higher value than adding more assertions because it protects every future test in the collection.

### H-4. Static Redis Multiplexer Escapes Fixture Lifetime

**Severity**: P1 (High)
**Location**: `tests/Hexalith.EventStore.Server.Tests/Events/EventPersistenceIntegrationTests.cs:29`
**Criterion**: Isolation / resource lifetime

`EventPersistenceIntegrationTests` owns a static `Lazy<Task<IConnectionMultiplexer>>`. It is not disposed with the collection fixture and can preserve connection/resource state outside the test lifetime.

**Recommendation**: move Redis connection ownership into a disposable fixture and clear EventStore-specific keys during setup. Do not use global `FLUSHDB` unless the fixture owns the whole database.

### H-5. Wall-Clock Timestamp Assertions Remain

**Severity**: P1 (High)
**Locations**:

- `tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyRecordTests.cs:24`
- `tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterMessageCompletenessTests.cs:200`

**Criterion**: Determinism
**Knowledge Base**: `timing-debugging.md`

The collector tests now prove the preferred pattern: inject `TimeProvider`, drive with `FakeTimeProvider`, and assert exact timestamps. A few actor/dead-letter tests still assert recency with `DateTimeOffset.UtcNow.AddSeconds(...)` or `AddMinutes(...)`.

**Recommendation**: extend the `TimeProvider` seam to idempotency/dead-letter creation paths or capture a fixed operation boundary time and assert relative ordering. Microsoft documents `FakeTimeProvider` specifically for deterministic time-dependent tests.

## Medium-Priority Recommendations

### M-1. Wrap Env-Var Fixture Startup In Try/Catch Restore

**Severity**: P2 (Medium)
**Locations**:

- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireContractTestFixture.cs:47`
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireProjectionFaultTestFixture.cs:30`
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/KeycloakAuthFixture.cs:45`
- `tests/Hexalith.EventStore.Server.Tests/Fixtures/DaprTestContainerFixture.cs:87`

These fixtures restore env vars in `DisposeAsync`, but if `InitializeAsync` throws before the fixture is fully initialized, cleanup is not guaranteed. `AspirePubSubProofTestFixture` already has the better pattern: snapshot values, wrap startup, restore in catch, and restore again in dispose.

### M-2. Replace Dapr Fixture Fixed Warmup With Readiness Polling

**Severity**: P2 (Medium)
**Location**: `tests/Hexalith.EventStore.Server.Tests/Fixtures/DaprTestContainerFixture.cs:104`

The fixture waits two seconds after sidecar health so app discovery and actor registration can settle. That is understandable, but it is still time-based. Poll the actual readiness condition with a bounded deadline instead.

### M-3. Share JwtAuthenticatedWebApplicationFactory Where Safe

**Severity**: P2 (Medium)
**Location**: `tests/Hexalith.EventStore.IntegrationTests/EventStore/ValidationTests.cs:23`

Nine EventStore integration classes use `IClassFixture<JwtAuthenticatedWebApplicationFactory>`, which means repeated factory construction. xUnit collection fixtures are designed for a shared context across classes. Use a collection fixture for read-only JWT factory tests, while keeping specialized rate-limiting factories separate.

### M-4. Move Admin UI E2E Selectors Toward Role/Test-ID Locators

**Severity**: P2 (Medium)
**Locations**:

- `tests/Hexalith.EventStore.Admin.UI.E2E/BrowserSmokeTests.cs:46`
- `tests/Hexalith.EventStore.Admin.UI.E2E/Dw5TypeCatalogNavigationBrowserAtddTests.cs:96`
- `tests/Hexalith.EventStore.Admin.UI.E2E/Dw5SidebarShortcutBrowserAtddTests.cs:47`

Playwright .NET locators auto-wait and are intended to represent user-facing semantics. CSS selectors are acceptable as a bridge, but navigation and command palette flows should prefer `GetByRoleAsync`/role locators or stable test IDs.

### M-5. Backfill Priority Traits For Selective Execution

**Severity**: P2 (Medium)
**Location**: `tests/Hexalith.EventStore.IntegrationTests/ContractTests/KeycloakAuthenticationTests.cs:21`

Only two `Priority` traits were found. This blocks a risk-based test selection strategy. Start with P0/P1 labels for auth, command lifecycle, tenant isolation, Dapr access control, and data protection sentinels.

### M-6. Convert Thread.Sleep Retry Loops To Async

**Severity**: P2 (Medium)
**Locations**:

- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/Dw5TypeCatalogRenderLoopAtddTests.cs:81`
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/PubSubDeliveryProofTests.cs:244`
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/PubSubDeliveryProofTests.cs:266`

Short sleeps are not catastrophic, but they are avoidable. Use bUnit `WaitForState`/`WaitForAssertion` for render stabilization and async cancellable retry for file-lock retries.

## Best Practices Found

### BP-1. FakeTimeProvider Collector Tests

**Location**: `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprHealthHistoryCollectorTests.cs:52`

This is the headline improvement since the May 4 review. Tests now instantiate `FakeTimeProvider`, pass it to the collector, and drive first capture deterministically. This aligns with Microsoft guidance for testing time-dependent code without waiting for actual time.

### BP-2. AspirePubSubProof Fixture Env Snapshot

**Location**: `tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspirePubSubProofTestFixture.cs:39`

The fixture snapshots environment variables before mutation and restores them in failure and dispose paths. Promote this into a shared `TestEnvScope` pattern.

### BP-3. bUnit As The Default UI Test Level

**Location**: `tests/Hexalith.EventStore.Admin.UI.Tests/AdminUITestContext.cs:23`

Admin UI component coverage largely stays in bUnit, reserving browser E2E for shell/navigation/accessibility proofs. This matches the test-level strategy: keep most checks at the lowest reliable level.

## Step Notes

- Mode: Create.
- Execution: Sequential. The workflow supports subagent-style workers, but this session did not explicitly request delegated agent work, so the four dimensions were run locally.
- Browser evidence collection: skipped. No target URL was provided for this suite-level static review, and no CLI/MCP browser session was opened. No browser session cleanup was required.
- Tests executed: none. This review is static analysis plus repository pattern inspection.
- Temp artifacts: saved under `_bmad-output/test-artifacts/.tea-tmp/` with timestamp `2026-05-20T09-23-20`.

## Knowledge And Official References

- `test-quality.md`: deterministic, isolated, explicit, focused, fast, self-cleaning test DoD.
- `data-factories.md`: factory/cleanup discipline.
- `test-levels-framework.md`: lower test levels before E2E when possible.
- `selector-resilience.md`: prefer resilient selectors over brittle CSS.
- `timing-debugging.md`: event-based waits over hard waits.
- Microsoft Learn, [Testing with FakeTimeProvider](https://learn.microsoft.com/en-us/dotnet/core/extensions/timeprovider-testing): confirms controllable time, instant advancement, and deterministic time-based tests.
- xUnit.net, [Sharing Context between Tests](https://xunit.net/docs/shared-context): collection fixtures share context across multiple test classes.
- Playwright .NET, [Locators](https://playwright.dev/dotnet/docs/locators): locators are the primary Playwright interaction model and support semantic querying/filtering.
- bUnit, [Writing tests for Blazor components](https://bunit.dev/docs/getting-started/writing-tests.html): bUnit renders components through `BunitContext` and runs through normal .NET test frameworks.

## Next Steps

1. Add a report-only or failing CI guard for oversized test files; start in warn mode if the 113-file backlog is too large to block immediately.
2. Promote `ActorTestHarness.AttachStateManager` and migrate duplicated reflection sites.
3. Extract `TestEnvScope` from `AspirePubSubProofTestFixture` and apply it to Aspire, Keycloak, and Dapr fixtures.
4. Reset Dapr collection fakes before each class/test in the shared collection.
5. Run `trace` next if you want coverage mapping and a gate decision; this review intentionally avoids scoring coverage.

## Decision

**Recommendation**: Approve with Comments.

There are no P0 test quality blockers. The suite is large, valuable, and improving in the right places, especially with the `FakeTimeProvider` repair. The next quality return comes from enforcing maintainability and isolation guardrails so new tests do not keep adding weight to already-large files and shared fixtures.

## Review Metadata

**Generated By**: BMad TEA Agent (Murat / Master Test Architect)
**Workflow**: `bmad-testarch-test-review`
**Execution Mode**: sequential
**Timestamp**: 2026-05-20T09-23-20
**Prior Review Archived As**: `_bmad-output/test-artifacts/archive/test-review-2026-05-04.md`
