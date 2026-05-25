---
reviewDate: '2026-05-25'
workflowType: 'testarch-framework (re-routed to audit)'
reviewScope: 'directory + integration wiring'
reviewTarget: 'tests/Hexalith.EventStore.Admin.UI.E2E/'
reviewer: 'Murat (TEA Agent) for Jerome'
priorReview: 'test-review-admin-ui-e2e-20260519.md (per-file quality)'
stackDetected: 'backend (.NET 10, .slnx, Microsoft.Playwright .NET wrapper + xUnit v3)'
---

# E2E Framework Audit — Hexalith.EventStore.Admin.UI.E2E

**Context:** `/bmad-testarch-framework` (Create) was invoked, but preflight HALTED — the project is
backend .NET (no Node `package.json`/`playwright.config.*`), and a mature E2E framework already
exists using the **Microsoft.Playwright .NET wrapper + xUnit v3 + Shouldly**. Scaffolding a Node
framework would be redundant and violates the project-context rule "Node dependencies are
release/workflow tooling only." Re-routed to an audit of the existing suite.

**Verdict:** The framework is well-built. Its problem was **orphaning** — it built and ran nowhere
automated. The critical gaps below were FIXED in this session; the carried-forward quality findings
remain open.

---

## 🔴 Critical findings (new — not caught by the 2026-05-19 per-file review)

| # | Finding | Evidence | Status |
|---|---------|----------|--------|
| C1 | **Project absent from the solution.** Not listed in `Hexalith.EventStore.slnx` (17 test projects were listed, not this one). Solution-level `dotnet build`/`dotnet test` skipped it. | `Hexalith.EventStore.slnx` | ✅ FIXED — added under `/tests/` |
| C2 | **Never built in CI.** `ci.yml` `Build` step builds the `.slnx`; the project was outside it, so it could rot/break while CI stayed green. | `.github/workflows/ci.yml` line ~118 | ✅ FIXED — now in `.slnx`, so the main job builds it |
| C3 | **Never executed in CI.** Tier 1 lists 11 projects explicitly (not this one); Tier 2 = `Server.Tests`, Tier 3 = `IntegrationTests`. No `playwright install` step existed. 16 active E2E tests gave **zero** regression protection. | `ci.yml` Tier 1/2/3 steps | ✅ FIXED — new `e2e-tests` job |

**Fix applied this session:**
- `.slnx`: added `tests/Hexalith.EventStore.Admin.UI.E2E`.
- `ci.yml`: new `e2e-tests` job (`needs: build-and-test`) — restore → build (Release) →
  `playwright.ps1 install --with-deps chromium` → `dotnet test` the **whole project** (no
  `Category=E2E` filter, so the untrait'd `SmokeTests` are not silently skipped) → TRX summary +
  failure artifact.
- Set `continue-on-error: true` for now (suite has never run on a Linux runner; mirrors the Tier 3
  `aspire-tests` stance). **Follow-up: flip to a hard gate once the first CI run is green on Linux.**
- Verified locally: E2E project compiles clean (0 warn / 0 err), `.slnx` parses with it included,
  `ci.yml` is valid YAML (5 jobs), `playwright.ps1` present at the install path.

---

## 🟡 Carried-forward quality findings — ✅ ALL FIXED 2026-05-25

From `test-review-admin-ui-e2e-20260519.md`; re-confirmed against current source.

| # | Finding | Location | Severity |
|---|---------|----------|----------|
| Q1 | **Dead code:** `AdminUIE2EFixture` has zero callers (grep-confirmed); misleading "install Playwright" comment though it uses none. Delete it. | `AdminUIE2EFixture.cs` | Medium |
| Q2 | **`SmokeTests` misclassified:** HTTP-only (`WebApplicationFactory` + `HttpClient` string-match), no browser, **no `[Trait("Category","E2E")]`**. Integration test in an E2E folder. | `SmokeTests.cs` | Medium |
| Q3 | **Brittle wall-clock perf asserts:** `ShellRendersWithin2Seconds` (2000 ms) and `Dashboard_ShellRendersWithin3Seconds` (3000 ms) — will flake under CI load / cold JIT. Most likely first Linux flake. | `SmokeTests.cs`, `BrowserSmokeTests.cs` | P0 brittle |
| Q4 | **Nav race:** `Navigation_CommandsPageLoads` clicks then asserts `page.Url` synchronously with no `WaitForURLAsync`. | `BrowserSmokeTests.cs` L56–71 | Medium |
| Q5 | **`PlaywrightFixture`:** TOCTOU port-allocation race; hard-coded `Headless=true` (no env switch for headed/slow-mo); **no trace/screenshot on failure** (biggest CI-debuggability gap). | `PlaywrightFixture.cs` | Medium |
| Q6 | **Config misalignment:** `tea_use_playwright_utils: true` + `test_framework: playwright` (Node) don't match the .NET reality. Set `tea_use_playwright_utils: false`. | `_bmad/tea/config.yaml` | Low |

## ✅ Strengths (no change needed)

- `Dw5TypeCatalogNavigationBrowserAtddTests` — deterministic waits (`WaitForURLAsync`, `Detached`),
  dual URL+visible-page assertions, bounded timeouts, console-error capture.
- `Dw5SidebarShortcutBrowserAtddTests` — hydration-aware shortcut-registration poll, viewport-tier
  storage-key check, persisted-boolean-vs-rendered-state guard against silent flip regressions.
- `Dw5DialogAccessibilityBrowserAtddTests` — honest `[Fact(Skip=...)]` deferral with seeding
  rationale and defensive multi-selector for Fluent web-components.

---

## Resolution (2026-05-25)

All six Q-findings fixed and verified — full suite re-run clean: **16 passed, 2 skipped, 0 failed**
(rebuilt Release, real Chromium).

- **Q1** — `AdminUIE2EFixture.cs` deleted.
- **Q2** — `[Trait("Category","E2E")]` added to `SmokeTests`; class doc clarified as HTTP (no browser).
- **Q3** — Wall-clock perf asserts **removed** (not just widened). The first run proved the point: the
  HTTP cold first-request took **14.7s** (cold JIT + Razor/static-asset warmup), tripping even a 10s
  "generous" bound — any fixed threshold is a guess. `SmokeTests.ShellRendersWithin2Seconds` →
  `ShellRendersOnColdFirstRequest` (status-only); `BrowserSmokeTests.Dashboard_ShellRendersWithin3Seconds`
  → `Dashboard_HydratesMainLandmark` (selector-only). AC-14 perf budget explicitly delegated to
  `perf-lab.yml` in comments.
- **Q4** — `Navigation_CommandsPageLoads` now `WaitForURLAsync("**/commands")` before the URL assertion.
- **Q5** — `PlaywrightFixture`: TOCTOU port race removed (bind `127.0.0.1:0`, read actual port from
  `IServerAddressesFeature` after start) + `HEXALITH_E2E_HEADED` / `HEXALITH_E2E_SLOWMO` env switches.
  **Still deferred:** trace/screenshot-on-failure (needs per-test outcome awareness — larger refactor).
- **Q6** — `tea_use_playwright_utils: false` in `_bmad/tea/config.yaml`.

## Recommended next actions (priority order)

1. **Watch the first CI run** on the next push to `main`; if green on Linux, flip `e2e-tests`
   `continue-on-error` → hard gate.
2. **Q3** — replace wall-clock perf asserts with deterministic readiness signals (or widen budgets
   / mark non-gating) before they flake in CI.
3. **Q1/Q2** — delete `AdminUIE2EFixture`; add `[Trait("Category","E2E")]` to `SmokeTests` or move
   it to an HTTP/integration project.
4. **Q5** — add `IBrowserContext.Tracing` on failure + an env switch for headed/slow-mo.
5. **Q4, Q6** — fix nav race; set `tea_use_playwright_utils: false`.
