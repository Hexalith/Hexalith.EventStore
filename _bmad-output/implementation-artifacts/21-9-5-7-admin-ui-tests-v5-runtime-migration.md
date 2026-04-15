# Story 21.9.5.7: Admin.UI.Tests bUnit Runtime v5 Migration (62 latent failures)

Status: ready-for-dev

**Parent story:** [21-9.5 Admin.UI.Tests Fluent UI v5 + bUnit v2 Migration](21-9-5-admin-ui-tests-v5-migration.md) — filed on trigger 2026-04-15 when compile-green unmasked 62 pre-existing runtime drift failures.

**Dependency:** None (21-9.5 closed at `review` with compile-green + AC 8 partial).

## Why this exists

21-9.5 ACs 1–7, 9, 10, 11 all passed (compile to 0 errors, grep-for-zero, slnx 36, Tier 1 753/753). AC 8 partially passed: the 3 21-8 merged-CSS smoke tests ran green on their first-ever execution. But the full Admin.UI.Tests bUnit suite revealed **62 runtime failures across ~16 test files** that had been latent since v4/v5 migration started (project never compiled to run them). These failures are not caused by 21-9.5's 5-file edits — they are independent v5 integration drift that surfaces now that the project builds.

Per ADR-21-9-5-004 (Approach A — surgical migration), 21-9.5 deliberately kept its scope small. Triaging 62 failures across 16 files is scope-expansion territory, pre-specified as a follow-up.

## Failure inventory (from 21-9.5 AC 8 test run 2026-04-15)

Test run output: `Failed: 62, Passed: 549, Skipped: 0, Total: 611`.

### Category A — NSubstitute/Castle.DynamicProxy gap on v5 `IToastService.ProviderId` (6 tests)

Failure signature: `System.TypeLoadException : Method 'set_ProviderId' in type 'Castle.Proxies.ObjectProxy_N' from assembly 'DynamicProxyGenAssembly2' does not have an implementation.`

Root cause: v5 `IToastService` adds a `ProviderId` member with a setter accessibility (likely `init` or non-virtual) that NSubstitute's Castle.DynamicProxy cannot proxy via `Substitute.For<IToastService>()`. All tests in `ToastServiceExtensionsTests` that mock `IToastService` hit this.

Failing tests:
- `ToastServiceExtensionsTests.ShowSuccessAsync_CallsShowToastAsync_WithSuccessIntent`
- `ToastServiceExtensionsTests.ShowErrorAsync_CallsShowToastAsync_WithErrorIntent`
- `ToastServiceExtensionsTests.ShowWarningAsync_CallsShowToastAsync_WithWarningIntent`
- `ToastServiceExtensionsTests.ShowInfoAsync_CallsShowToastAsync_WithInfoIntent`
- `ToastServiceExtensionsTests.ShowSuccessAsync_NullMessage_PassesNullBodyWithoutThrowing`
- `ToastServiceExtensionsTests.ShowSuccessAsync_WhenShowToastAsyncThrows_PropagatesException`

Likely fix approach: switch from `Substitute.For<IToastService>()` to a hand-rolled fake `TestToastService : IToastService` that captures calls in a list, OR upgrade Castle.Core to a version that supports the new member shape.

### Category B — v5 structural regression: `NavMenu_RendersV4StructuralElements` (1 test)

Assertion: `markup.ShouldContain("fluent-nav-menu")`, `fluent-nav-link`, `fluent-nav-group`. These are v4 kebab tag names. v5 renders `fluent-nav`, `fluent-nav-item`, `fluent-nav-category` (the test comment itself documents the rename). The test was authored as a v4-baseline regression marker in 21-0 and is NOW the canary that fires.

Failing test:
- `NavMenuTests.NavMenu_RendersV4StructuralElements`

Fix: rename assertions to v5 tags or delete the test (migration canary complete).

### Category C — `NumericInput` rendering drift (2 tests)

Two NumericInputTests fail. Likely: the `<fluent-text-input>` tag used in v4 is now `<fluent-text-input>` still but with different attribute naming (`current-value` → `value` or similar). Story 21-9.5 preserved the asserted strings; v5 may emit different attribute names.

Failing tests:
- `NumericInputTests.NumericInput_RendersWithInitialValue`
- `NumericInputTests.NumericInput_RoundTripsValueThroughChange`

Fix: inspect actual markup from `cut.Markup` and update assertions to match v5 attribute naming.

### Category D — EventDebugger + CommandSandbox + BisectTool test suites (26 tests)

These 3 components are heavy FluentUI consumers (buttons, dialogs, lists, selectors). Failures likely span multiple v5 migration drift points: Slot API changes (21-6), Appearance enum (21-3/21-4), Toast invocation contract (21-7).

Failing tests: 14 in EventDebuggerTests, 6 in CommandSandboxTests, 6 in BisectToolTests.

Fix approach: per-component triage. Some may share root cause with Category A (NSubstitute + v5 member additions).

### Category E — Pages/* test failures (18 tests)

TenantsPage, SnapshotsPage, ConsistencyPage, DeadLettersPage, CompactionPage, StreamDetailPage tests. Likely mix of v5 dialog/toast/form drift.

Failing tests: 6 in TenantsPageTests, 3 in SnapshotsPageTests, 2 in ConsistencyPageTests, 4 in DeadLettersPageTests, 2 in CompactionPageTests, 1 in StreamDetailPageTests.

### Category F — Shared infrastructure failures (9 tests)

- `CommandPaletteTests` (3) — v5 dialog slot changes
- `StateInspectorModalTests` (3) — v5 dialog slot changes
- `StatCardTests.StatCard_AppliesSeverityBasedInlineColorStyle(neutral)` (1 — single theory case) — v5 CSS variable rename from 21-8
- `HostBootstrapTests.BlazorServerHost_BootstrapsWithoutErrors` (1) — 500 InternalServerError on `/` (v5 service registration issue in test host bootstrap)
- `BreadcrumbTests.Breadcrumb_CopyButton_InvokesJSInterop` (1) — JSInterop v5 signature change

## Acceptance Criteria

1. `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release --no-build` reports `Failed: 0`, preserving the 549 already-passing tests + resolving 62 failures (total 611).
2. No test is `[Fact(Skip=...)]` unless the skip message cites `21-9.5.7` or a further follow-up key (no blanket skips).
3. Tier 1 non-UI regression remains 753/753 green.
4. Slnx residual errors remain 36 (Sample.BlazorUI only, 21-10 scope) — this story is test-only.

## Out of scope

- Production code in `src/Hexalith.EventStore.Admin.UI/` (migrated in 21-1..21-9).
- `samples/Hexalith.EventStore.Sample.BlazorUI/` (21-10 scope).
- Adding new tests.

## References

- [21-9.5 story + Completion Notes](21-9-5-admin-ui-tests-v5-migration.md) — parent, full 62-failure list in its test run log.
- `_bmad-output/implementation-artifacts/deferred-work.md` — this story is referenced there.
