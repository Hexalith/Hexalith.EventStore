---
stepsCompleted: ['step-01-load-context', 'step-02-discover-tests']
lastStep: 'step-02-discover-tests'
lastSaved: '2026-05-19'
workflowType: 'testarch-test-review'
reviewScope: 'directory'
reviewTarget: 'tests/Hexalith.EventStore.Admin.UI.E2E/'
priorReview: '../test-review.md (full-suite 2026-05-04)'
inputDocuments:
  - .claude/skills/bmad-testarch-test-review/resources/knowledge/test-quality.md
  - .claude/skills/bmad-testarch-test-review/resources/knowledge/data-factories.md
  - .claude/skills/bmad-testarch-test-review/resources/knowledge/test-levels-framework.md
  - .claude/skills/bmad-testarch-test-review/resources/knowledge/selective-testing.md
  - .claude/skills/bmad-testarch-test-review/resources/knowledge/test-healing-patterns.md
  - .claude/skills/bmad-testarch-test-review/resources/knowledge/selector-resilience.md
  - .claude/skills/bmad-testarch-test-review/resources/knowledge/timing-debugging.md
  - _bmad-output/project-context.md
  - tests/Hexalith.EventStore.Admin.UI.E2E/Hexalith.EventStore.Admin.UI.E2E.csproj
---

# Test Quality Review: Hexalith.EventStore.Admin.UI.E2E

**Quality Score**: TBD/100 (TBD)
**Review Date**: 2026-05-19
**Review Scope**: directory
**Reviewer**: Murat (TEA Agent) for Jerome

> Output path note: the skill template references `{test_artifacts}/test-review.md`, but that path already holds a completed suite-scope review from 2026-05-04. To preserve that artifact, this directory-scope review is filed under the project's configured `test_review_output: _bmad-output/test-artifacts/test-reviews/`.

---

Note: This review audits existing tests; it does not generate tests. Coverage mapping and coverage gates are out of scope; route those concerns to the `trace` workflow.

## Step 01 — Load Context Output

### Scope

- **Target directory**: `tests/Hexalith.EventStore.Admin.UI.E2E/`
- **Review type**: directory (all test files in the folder against quality criteria)
- **Stack detection**: `backend` (.NET 10, `.csproj` manifests, `.slnx` solution). The test directory exercises browser-driven UI via the `Microsoft.Playwright` .NET wrapper.

### Stack Reality vs. Config Misalignment (Flagged)

- `tea_use_playwright_utils: true` in `_bmad/tea/config.yaml` references the JS/TS `playwright-utils` npm library. The project uses `Microsoft.Playwright` (.NET wrapper). Knowledge fragments specific to the JS library (`overview.md`, `network-recorder.md`, `intercept-network-call.md`, `log.md`, `file-utils.md`, `burn-in.md`, `network-error-monitor.md`, `fixtures-composition.md`) were intentionally skipped — their APIs do not translate 1:1 to .NET. Recommend setting `tea_use_playwright_utils: false` for this project.
- `tea_use_pactjs_utils: true` and `tea_pact_mcp: mcp`: no contract tests in scope; specialized Pact fragments skipped.
- `tea_browser_automation: auto`: `playwright-cli.md` (JS CLI) skipped; .NET equivalent is invoked via `pwsh bin/Debug/net10.0/playwright.ps1`.

### Knowledge Fragments Loaded (Core Tier — Framework-Agnostic Patterns)

1. `test-quality.md` — Definition of Done (deterministic, isolated, <300 lines, <1.5 min, self-cleaning)
2. `data-factories.md` — Factory functions with overrides, API-first setup
3. `test-levels-framework.md` — Unit vs integration vs E2E selection
4. `selective-testing.md` — Tag/priority filtering, diff-based runs, promotion rules
5. `test-healing-patterns.md` — Stale selector, race condition, dynamic data, network, hard-wait remediation
6. `selector-resilience.md` — `data-testid > ARIA > text > CSS/ID` hierarchy
7. `timing-debugging.md` — Network-first pattern, deterministic waits

Extended-tier fragments (`fixture-architecture.md`, `network-first.md`, `playwright-config.md`, `ci-burn-in.md`) deferred to load on-demand during step-03 subagent execution if a specific finding requires them.

### Context Artifacts Gathered

- **Project context**: `_bmad-output/project-context.md` — .NET 10, Aspire, DAPR, Blazor Fluent UI admin, xUnit v3 + Shouldly + NSubstitute, `EnableKeycloak=false` dev mode with HS256 JWT.
- **Prior review**: `_bmad-output/test-artifacts/test-review.md` (2026-05-04, full-suite, raw 38/100 / calibrated B+ 86/100). Will be cross-referenced — not re-evaluated.
- **Test project manifest**: `tests/Hexalith.EventStore.Admin.UI.E2E/Hexalith.EventStore.Admin.UI.E2E.csproj` references `Microsoft.Playwright`, `Microsoft.AspNetCore.Mvc.Testing`, `xunit.v3`, `Shouldly`, `coverlet.collector`. NuGet audit mode pinned to `direct` due to Playwright transitive deps.
- **Test files in scope (9)**:
  - `GlobalUsings.cs` (global usings)
  - `PlaywrightCollection.cs` (xUnit collection definition)
  - `PlaywrightFixture.cs` (Kestrel + Chromium fixture)
  - `AdminUIE2EFixture.cs` (`WebApplicationFactory<Program>`-only fixture)
  - `SmokeTests.cs` (HTTP-only landing page checks)
  - `BrowserSmokeTests.cs` (real browser smoke)
  - `Dw5DialogAccessibilityBrowserAtddTests.cs`
  - `Dw5TypeCatalogNavigationBrowserAtddTests.cs`
  - `Dw5SidebarShortcutBrowserAtddTests.cs`
- **No prior test-design or trace artifacts** found for the Admin.UI.E2E slice. Priorities will be inferred from naming (ATDD/smoke) and risk impact during step-03.

### Preliminary Observations (To Be Verified in Step 02–03)

These will be confirmed or refuted by discovery and quality evaluation; recording here for audit trail.

1. **`SmokeTests.cs` is misnamed.** Uses `IClassFixture<WebApplicationFactory<Program>>` directly with `HttpClient` content-string inspection — no Playwright, no browser. This is HTTP/integration testing in an "E2E" folder.
2. **Two fixtures overlap.** `AdminUIE2EFixture` (`Mvc.Testing`-only) and `PlaywrightFixture` (Kestrel + Chromium) coexist. Need to verify each is used purposefully or whether one is dead code.
3. **Brittle perf assertion in `SmokeTests.ShellRendersWithin2Seconds`** — hard 2000 ms cutoff on `HttpClient.GetAsync()` round-trip. Wall-clock-tied; will flake under CI load, cold JIT, or first-request setup.
4. **TOCTOU race in `PlaywrightFixture` port allocation** — opens a socket on `IPEndPoint(Loopback, 0)`, reads the assigned port, closes the socket, hands the port to Kestrel. Between close and bind another process can claim it.
5. **No headed/debug switch in `PlaywrightFixture`** — `Headless = true` is hard-coded; debugging requires a code change.

---

## Step 02 — Discover & Parse Tests

### File Inventory (9 files in `tests/Hexalith.EventStore.Admin.UI.E2E/`)

| File | Lines | Tests | Skipped | Fixture | Category Trait | Kind |
|---|---|---|---|---|---|---|
| `GlobalUsings.cs` | 5 | 0 | — | — | — | Global usings |
| `PlaywrightCollection.cs` | 8 | 0 | — | — | — | xUnit `ICollectionFixture<PlaywrightFixture>` |
| `PlaywrightFixture.cs` | 125 | 0 | — | — | — | Kestrel + Chromium fixture (`IAsyncLifetime`) |
| `AdminUIE2EFixture.cs` | 38 | 0 | — | — | — | `WebApplicationFactory<Program>` fixture |
| `SmokeTests.cs` | 77 | 4 | 0 | `WebApplicationFactory<Program>` (IClassFixture) | ❌ missing | HTTP-only (no browser) |
| `BrowserSmokeTests.cs` | 105 | 5 | 0 | `PlaywrightFixture` (Collection) | ✅ `"E2E"` | Real browser smoke |
| `Dw5DialogAccessibilityBrowserAtddTests.cs` | 123 | 2 | 2 | `PlaywrightFixture` (Collection) | ✅ `"E2E"` | ATDD red-phase (skip-guarded) |
| `Dw5TypeCatalogNavigationBrowserAtddTests.cs` | 120 | 4 | 0 | `PlaywrightFixture` (Collection) | ✅ `"E2E"` | ATDD live (nav + console) |
| `Dw5SidebarShortcutBrowserAtddTests.cs` | 179 | 3 | 0 | `PlaywrightFixture` (Collection) | ✅ `"E2E"` | ATDD live (Ctrl+B, Ctrl+K, storage) |

**Totals**: 18 test methods (16 active, 2 skipped) across 5 test classes; 3 infrastructure files; 1 confirmed dead-code fixture.

### Per-File Findings

#### `PlaywrightFixture.cs` (infrastructure)

- **Port allocation TOCTOU race** (lines 33–37): opens a socket on `IPEndPoint(Loopback, 0)`, reads the OS-assigned port, then closes the socket and hands the port number to Kestrel. Between socket close and Kestrel bind, the port is free for any other process to claim. Probability low on a quiet dev box, non-zero under parallel CI matrices.
- **Hard-coded `Headless = true`** (line 85). Any debug iteration requires editing the fixture.
- **`AddServiceDefaults` skipped** (line 57 comment). The fixture builds its own minimal host instead of using the production service-defaults stack; this is documented but means the test environment diverges from prod composition.
- **Server-up probe is permissive** (lines 74–80): accepts both `IsSuccessStatusCode` and `404 NotFound` as "ready". A 404 here masks a misconfigured static-assets manifest. Reasonable but worth a comment about the intent.
- **No tracing/screenshot on failure**. Failures will only have stack traces — no DOM snapshot, no console log, no network log. Debugging from CI logs alone will be painful.
- **No environment switch for headed mode**, slow-mo, video, or trace capture. All hard-coded.

#### `AdminUIE2EFixture.cs` ⚠️ **Dead code (confirmed)**

- Grep across the entire repo: zero callers in test code. Only references are this file's declaration and planning-artifact docs.
- Misleading comment (lines 8–11) instructs users to install Playwright browsers — this fixture doesn't use Playwright.
- Recommend **deletion**.

#### `SmokeTests.cs` ⚠️ **Misclassified location**

- Uses `IClassFixture<WebApplicationFactory<Program>>` with `HttpClient` content-string inspection. **No browser. Not E2E.** This is an HTTP/integration test sitting in a folder named `Admin.UI.E2E`.
- **No `[Trait("Category", "E2E")]`** — the other tests in the directory carry it; selective execution by `Category=E2E` will skip these silently.
- `ShellRendersWithin2Seconds` (lines 62–76): hard 2 000 ms wall-clock budget on `HttpClient.GetAsync()`. Will flake on first-request JIT, cold WAF, or busy CI runners. P0 brittle perf assertion.
- Tests inspect raw HTML strings (`content.ShouldContain("Hexalith EventStore Admin")`, `content.ShouldContain("lang=\"en\"")`) — couples tests to render output rather than semantic structure. Acceptable for a smoke test but breaks on copy or i18n changes.
- Recommend **moving these to a separate `Hexalith.EventStore.Admin.UI.Http.Tests` project** (or back to `Admin.UI.Tests`), or at minimum renaming the class to make the HTTP-only nature explicit.

#### `BrowserSmokeTests.cs` ✅ Solid baseline, two issues

- 5 tests, all `[Fact]`, `[Collection("Playwright")]`, `[Trait("Category", "E2E")]`. Each creates a fresh `IBrowserContext` per test (good isolation).
- **Brittle perf assertion** (`Dashboard_ShellRendersWithin3Seconds`, lines 74–89): same pattern as `SmokeTests` — Stopwatch + 3 000 ms cutoff. Slightly less brittle than the 2 s version but same class of issue.
- **`Navigation_CommandsPageLoads` race-prone** (lines 56–71): clicks `.admin-sidebar nav a[href='/commands']` then asserts `page.Url.ShouldContain("/commands")` **synchronously**. No `WaitForURLAsync` between click and URL check. In a Blazor Server app the URL update is event-driven; this can pass before the server commits, or fail because the click hasn't fully processed.
- **Selector strategy mixes CSS and ARIA**. `.admin-sidebar nav[aria-label='Main navigation']` couples to a CSS class. The Admin.UI doesn't appear to expose `data-testid` consistently — this is a project-wide design question, not a per-test fix.
- **No tracing on failure**.

#### `Dw5DialogAccessibilityBrowserAtddTests.cs` ✅ Honest deferral

- Two `[Fact(Skip = ...)]` tests with detailed skip reasons explaining (a) what's blocked, (b) the seeding requirement, (c) when to remove Skip.
- **Defensive multi-selector** for Fluent web-component (`fluent-dialog[aria-label='Event payload'], [role='dialog'][aria-label='Event payload']`) — survives Fluent UI renderer changes.
- **Helper methods throw** when the scaffold isn't ready (lines 106–122). The Skip prevents the throws from firing in normal runs, but if someone removes Skip without seeding, the throw makes the failure mode crystal clear. Reasonable.
- **No test-ID format** (`{EPIC}.{STORY}-{LEVEL}-{SEQ}`) — but the story reference (`DW5 AC#8` / `AC#9`) acts as a stable identifier.
- **Comment-as-spec** for "what the scaffold pins vs. what the dev's evidence pass provides" — excellent governance.

#### `Dw5TypeCatalogNavigationBrowserAtddTests.cs` ✅ Best-in-class

- 4 `[Fact]` tests, each starting from a different `/types` URL. Shared parameterized helper.
- **Network-aware deterministic waits** throughout — `WaitForURLAsync`, `LocatorWaitForOptions { State = Detached }`, `WaitForSelectorAsync`. Zero hard waits.
- **Dual-assertion guard** (URL **AND** visible page transition) — explicit comment captures the failure mode being guarded against.
- **Bounded `_navTimeout = 3s`** — matches BrowserSmokeTests budget. Justified and bounded.
- **Console error capture** (`page.Console += ...`) — collects error-level messages and asserts empty at end. Standard Playwright pattern.
- **Re-arm pattern** (`await page.GotoAsync(startingUrl)` between iterations) — could be expensive (multiple full reloads), but the cost buys deterministic isolation per nav target.
- **Minor**: could be `[Theory]` with `InlineData` for the three starting URLs, but the current style is more readable for ATDD.

#### `Dw5SidebarShortcutBrowserAtddTests.cs` ✅ Excellent technical depth

- 3 `[Fact]` tests covering AC#5, #6, #7.
- **`WaitForShortcutRegistrationAsync` helper** (lines 173–178) — polls `window.hexalithAdmin?._shortcutHandlers?.size > 0` before pressing keys. Eliminates hydration race. **Excellent pattern.**
- **`CtrlB_StorageKey_MatchesViewportTier_AndPersistsAcrossRefresh`** — viewport-tier computation in test, persistence check, AND visible-state-must-match-persisted-boolean assertion. Explicit comment about guarding against silent boolean-flip regression. **Top-tier defensive testing.**
- **`CtrlK_OpenCloseReopen_...`** — three-step Ctrl+K open / Esc close / Ctrl+K re-open in same session. Regression guard for Story 21-13. Uses multi-selector for Fluent text field.
- **Minor gap in `CtrlB_RepeatedToggle_...`**: presses Ctrl+B five times, asserts class changes each iteration AND no console errors, but **doesn't assert final state matches the parity of 5 presses** (5 = odd ⇒ should end collapsed). A regression that increments the toggle twice per press would pass this test. Minor — fold into "should improve" rather than "must fix".
- **Coupling to `window.hexalithAdmin`** global — fragile if app's JS interop name changes. Documented elsewhere in the codebase per project convention.

### Evidence Collection — Skipped

- Step requires `playwright-cli` (JS package) when `tea_browser_automation` is `"auto"`. Project uses .NET Playwright via `Microsoft.Playwright` NuGet; the JS CLI is not installed. .NET equivalent (built-in `IBrowserContext.Tracing.StartAsync()`) would require booting the full Aspire/Admin.UI stack — out of scope for a static review.
- Static analysis of test source is sufficient for this review.

### Cross-Cutting Observations

- **No data factories or API seeding**. ATDD tests rely on the Admin.UI rendering correctly cold-start, with placeholder navigation comments noting "dev's evidence pass must replace this with the concrete Aspire-seeded path". This is a documented limitation, not a violation — the project hasn't yet wired a state-seed path through the fixture.
- **No `data-testid` attributes**. Tests reach for CSS classes (`.admin-sidebar`, `.stat-card-grid`, `.skip-to-main`) and ARIA (`role='main'`, `aria-label='Main navigation'`). Mixed strategy is documented in test-review.md (2026-05-04) at the project level. Improving this is a UI-side investment, not a test-only fix.
- **No tracing on failure** anywhere in the suite. `Microsoft.Playwright`'s `IBrowserContext.Tracing` API is available but unused. Single biggest leverage point for CI debuggability.
- **No headed/slow-mo debug mode** in the fixture. Environment-variable switch (`HEXALITH_E2E_HEADED=1`, `HEXALITH_E2E_SLOWMO=250`) would cost ~10 lines and rescue every future debugging session.
- **`Hexalith.EventStore.Testing` helpers not consulted** — there may be DAPR/Keycloak fakes that could seed the dialog/EventDebugger scaffolds, removing the Skip-guards. Worth investigating in step-03.

---

