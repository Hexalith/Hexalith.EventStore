---
stepsCompleted:
  - step-01-preflight-and-context
  - step-02-generation-mode
  - step-03-test-strategy
  - step-04-generate-tests
  - step-04c-aggregate
  - step-05-validate-and-complete
lastStep: step-05-validate-and-complete
lastSaved: '2026-05-05'
storyId: DW5
storyKey: post-epic-deferred-dw5-admin-ui-runtime-follow-ups
storyFile: _bmad-output/implementation-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md
atddChecklistPath: _bmad-output/test-artifacts/atdd-checklist-post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md
generatedTestFiles:
  - tests/Hexalith.EventStore.Admin.UI.Tests/Layout/Dw5SidebarShortcutAtddTests.cs
  - tests/Hexalith.EventStore.Admin.UI.Tests/Pages/Dw5TypeCatalogRenderLoopAtddTests.cs
  - tests/Hexalith.EventStore.Admin.UI.Tests/Pages/Dw5TypeCatalogUrlIdempotencyAtddTests.cs
  - tests/Hexalith.EventStore.Admin.UI.Tests/Components/Dw5DialogAccessibilityAtddTests.cs
  - tests/Hexalith.EventStore.Admin.UI.Tests/Components/Dw5FluentV5InvariantsAtddTests.cs
  - tests/Hexalith.EventStore.Admin.UI.Tests/Governance/Dw5GovernanceAtddTests.cs
  - tests/Hexalith.EventStore.Admin.UI.Tests/Dw5TestPaths.cs
  - tests/Hexalith.EventStore.Admin.UI.E2E/Dw5TypeCatalogNavigationBrowserAtddTests.cs
  - tests/Hexalith.EventStore.Admin.UI.E2E/Dw5SidebarShortcutBrowserAtddTests.cs
  - tests/Hexalith.EventStore.Admin.UI.E2E/Dw5DialogAccessibilityBrowserAtddTests.cs
inputDocuments:
  - _bmad-output/implementation-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md
  - _bmad-output/implementation-artifacts/post-epic-deferred-dw4-operational-evidence-schema-validation.md
  - _bmad/tea/config.yaml
  - .claude/skills/bmad-testarch-atdd/resources/tea-index.csv
  - tests/Hexalith.EventStore.Admin.UI.Tests/Layout/MainLayoutTests.cs
  - tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TypeCatalogPageTests.cs
  - tests/Hexalith.EventStore.Admin.UI.Tests/Components/CommandSandboxTests.cs
  - tests/Hexalith.EventStore.Admin.UI.Tests/Components/EventDebuggerTests.cs
  - tests/Hexalith.EventStore.Admin.UI.E2E/BrowserSmokeTests.cs
  - tests/Hexalith.EventStore.Admin.UI.E2E/SmokeTests.cs
  - tests/Hexalith.EventStore.Admin.UI.E2E/PlaywrightFixture.cs
  - tests/Hexalith.EventStore.Admin.UI.E2E/AdminUIE2EFixture.cs
detectedStack: fullstack
testFrameworks:
  - bUnit (component, xUnit + Shouldly + NSubstitute)
  - Microsoft.Playwright (.NET browser E2E, xUnit)
generationMode: ai-generation
---

# DW5 ATDD Checklist

## Step 1: Preflight & Context

### Story
- **Key:** `post-epic-deferred-dw5-admin-ui-runtime-follow-ups`
- **ID:** DW5
- **Status:** ready-for-dev
- **Acceptance criteria:** 15 numbered behaviors covering TypeCatalog navigation, Ctrl+B/Ctrl+K shortcuts, dialog accessibility, evidence artifacts, deferred-work dispositions, scope boundaries.

### Stack Detection
- Auto-detected: `fullstack` (.NET 10 backend + Blazor Server frontend).
- DW5 work is overwhelmingly **UI runtime** (Blazor renderer-context, Fluent UI v5 components, sidebar shortcuts, dialog markup).

### Test Frameworks
- **bUnit** — `tests/Hexalith.EventStore.Admin.UI.Tests/` for deterministic component contracts.
- **Microsoft.Playwright (.NET)** — `tests/Hexalith.EventStore.Admin.UI.E2E/` for browser runtime evidence.
- No `playwright.config.ts`; .NET Playwright is invoked via `PlaywrightFixture`/`AdminUIE2EFixture`.

### Knowledge Fragments Loaded
- Core: `data-factories`, `component-tdd`, `test-quality`, `test-healing-patterns`.
- Frontend: `selector-resilience`, `timing-debugging`.
- Backend/fullstack: `test-levels-framework`, `test-priorities-matrix`.
- Skipped: Playwright Utils (TS-only), Pact (no contract testing in DW5), MCP recording (not used).

### TEA Config Flags
- `test_framework: playwright`
- `tea_use_playwright_utils: true` → not applicable (TS-only library, scope is .NET Playwright).
- `tea_browser_automation: auto` → use existing `PlaywrightFixture` C# pattern.

## Step 2: Generation Mode

**Mode: AI Generation** (no live browser recording).

**Rationale:**
- ACs are clear and decomposable into discrete failing-test behaviors.
- Repository already has fixture and bUnit conventions to mirror.
- Story mandates evidence-first stop rules; pre-recorded selectors would conflict with "do not patch from hypothesis alone".
- C# Playwright .NET makes TS-only recording tools (CLI, MCP) inappropriate.

## Step 3: Test Strategy (AC → Level → Priority → File)

| AC | Behavior | Level | Priority | Test File |
| --- | --- | --- | ---: | --- |
| #1 | DW5 baselined from real deferred entries (decision ledger present in evidence folder) | Governance (file-system) | P3 | `tests/Hexalith.EventStore.Admin.UI.Tests/Governance/Dw5GovernanceAtddTests.cs` |
| #2 | TypeCatalog sidebar nav from `/types?tab=*` reproduces or closes with URL + visible page evidence | E2E (Playwright .NET) | P0 | `tests/Hexalith.EventStore.Admin.UI.E2E/Dw5TypeCatalogNavigationBrowserAtddTests.cs` |
| #3 | TypeCatalog render-loop hypotheses tested before broad rewrites (rapid tab toggles do not throw, no redirect-loop signal) | bUnit | P2 | `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/Dw5TypeCatalogRenderLoopAtddTests.cs` |
| #4 | TypeCatalog deep-link / tab / type= URL initialization + UpdateUrl idempotency | bUnit | P0 | `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/Dw5TypeCatalogUrlIdempotencyAtddTests.cs` |
| #5 | Ctrl+B sidebar toggle works on Blazor renderer context (bUnit contract + browser repeat without circuit errors) | bUnit + E2E | P0 | `Layout/Dw5SidebarShortcutAtddTests.cs` (bUnit) + `Dw5SidebarShortcutBrowserAtddTests.cs` (E2E) |
| #6 | Ctrl+B persistence is viewport-tier-scoped (`hexalith-sidebar-collapsed-{tier}`) and survives refresh | bUnit + E2E | P1 | combined with #5 files |
| #7 | Ctrl+K palette remains non-regressive in same session as Ctrl+B | E2E | P0 | combined with #5 E2E file |
| #8 | CommandSandbox dialog has `aria-label="Event payload"` on rendered Fluent dialog (markup + live DOM) | bUnit + E2E | P1 | `Components/Dw5DialogAccessibilityAtddTests.cs` (bUnit) + `Dw5DialogAccessibilityBrowserAtddTests.cs` (E2E) |
| #9 | EventDebugger dialog has `aria-label="Event payload"` on rendered Fluent dialog | bUnit + E2E | P1 | combined with #8 files |
| #10 | Runtime evidence artifacts durable: folder + index file with required columns | Governance | P2 | `Governance/Dw5GovernanceAtddTests.cs` |
| #11 | Smallest useful mix of bUnit + browser coverage | (meta — covered by the test mix itself) | — | — |
| #12 | Fluent UI Blazor v5 invariants (no `Typo`/`Typography`, `FluentDialogBody` retained, `FluentTabs ActiveTabId/ActiveTabIdChanged` retained) | bUnit (markup contract) | P2 | `Components/Dw5FluentV5InvariantsAtddTests.cs` |
| #13 | Deferred-work.md DW5 disposition markers narrow & auditable | Governance | P2 | `Governance/Dw5GovernanceAtddTests.cs` |
| #14 | Scope boundaries — File List entries stay under allowed UI/test/evidence roots | Governance | P2 | `Governance/Dw5GovernanceAtddTests.cs` |
| #15 | Bookkeeping — Dev Agent Record / Change Log / Verification Status updated by dev handoff | Governance | P3 | `Governance/Dw5GovernanceAtddTests.cs` |

### Red-Phase Discipline

- Every scaffold uses `[Fact(Skip = "ATDD red phase — DW5 AC#X (...). Remove Skip when ...")]`.
- Skip messages name the AC and the action that unmarks it.
- Build clean: 0 warnings, 0 errors. Runtime: all skipped, 0 passed, 0 failed.
- Mirrors DW2/DW3/DW4 conventions: file-header story reference, per-AC skip constants, `Shouldly` assertions with `customMessage` documenting the AC contract.

### Files To Generate (9 total)

**bUnit (`tests/Hexalith.EventStore.Admin.UI.Tests/`):**

1. `Layout/Dw5SidebarShortcutAtddTests.cs` — AC #5, #6, #7 deterministic parts
2. `Pages/Dw5TypeCatalogRenderLoopAtddTests.cs` — AC #3
3. `Pages/Dw5TypeCatalogUrlIdempotencyAtddTests.cs` — AC #4
4. `Components/Dw5DialogAccessibilityAtddTests.cs` — AC #8, #9 markup contract
5. `Components/Dw5FluentV5InvariantsAtddTests.cs` — AC #12
6. `Governance/Dw5GovernanceAtddTests.cs` — AC #1, #10, #13, #14, #15

**E2E (`tests/Hexalith.EventStore.Admin.UI.E2E/`):**

7. `Dw5TypeCatalogNavigationBrowserAtddTests.cs` — AC #2
8. `Dw5SidebarShortcutBrowserAtddTests.cs` — AC #5, #6, #7 browser-runtime parts
9. `Dw5DialogAccessibilityBrowserAtddTests.cs` — AC #8, #9 live DOM

## Step 4 + 4C: Generate & Aggregate (Red Phase)

### bUnit project (Hexalith.EventStore.Admin.UI.Tests)

- **Build:** Release, 0 warnings, 0 errors.
- **Runtime (DW5 filter):** 25 skipped, 0 passed, 0 failed.
- **Full project:** 622 passed, 25 skipped, 0 failed (no regressions).
- **Files added (7):**
  - `Layout/Dw5SidebarShortcutAtddTests.cs` (4 facts + 1 theory) — AC #5, #6, #7
  - `Pages/Dw5TypeCatalogRenderLoopAtddTests.cs` (2 facts) — AC #3
  - `Pages/Dw5TypeCatalogUrlIdempotencyAtddTests.cs` (5 facts) — AC #4
  - `Components/Dw5DialogAccessibilityAtddTests.cs` (4 facts) — AC #8, #9 markup
  - `Components/Dw5FluentV5InvariantsAtddTests.cs` (3 facts) — AC #12
  - `Governance/Dw5GovernanceAtddTests.cs` (5 facts) — AC #1, #10, #13, #14, #15
  - `Dw5TestPaths.cs` (helper)

### E2E project (Hexalith.EventStore.Admin.UI.E2E) — IMPORTANT CAVEAT

- **Files added (3):**
  - `Dw5TypeCatalogNavigationBrowserAtddTests.cs` — AC #2 (4 facts)
  - `Dw5SidebarShortcutBrowserAtddTests.cs` — AC #5, #6, #7 browser parts (3 facts)
  - `Dw5DialogAccessibilityBrowserAtddTests.cs` — AC #8, #9 live DOM (2 facts)
- **Pre-existing condition (NOT introduced by DW5 ATDD):**
  - The entire `tests/Hexalith.EventStore.Admin.UI.E2E/` folder is **gitignored** by the
    `*.e2e` pattern at `.gitignore` line 137 (matches the substring `.e2e` inside the
    `Admin.UI.E2E` directory name). The folder contains untracked source files including
    `Hexalith.EventStore.Admin.UI.E2E.csproj`.
  - The csproj declares `<PackageReference Include="xunit" />` but `Directory.Packages.props`
    only defines `xunit.v3` versions, so the project fails CPM restore (NU1010).
  - The project is **not in `Hexalith.EventStore.slnx`**, so CI does not see it.
- **Implication:** The E2E scaffolds pin the runtime contract for the dev's manual browser
  evidence pass (which the story explicitly mandates) and serve as a runnable target if and
  when the E2E project is properly tracked. They do not gate CI today and the dev should:
  - Capture browser evidence for AC #2, #5, #6, #7, #8 live DOM, and #9 live DOM through the
    manual pass described in story Tasks 1.1–1.8, 2.1–2.7, and 3.1–3.7, AND
  - Either fix the E2E project tracking + CPM as a separate concern (out of DW5 scope), OR
  - Leave the E2E scaffolds as documented intent and rely on bUnit + manual evidence.

### Coverage Summary by AC

| AC | bUnit | E2E (untracked) | Manual evidence | Coverage source |
| --- | :-: | :-: | :-: | --- |
| #1 | ✅ | — | — | governance ledger gate |
| #2 | — | ✅ (untracked) | required | manual browser pass |
| #3 | ✅ | — | — | hypothesis-guard tests |
| #4 | ✅ | — | — | URL idempotency + deep-link tests |
| #5 | ✅ | ✅ (untracked) | required for live circuit | bUnit contract + manual browser |
| #6 | ✅ | ✅ (untracked) | required for refresh | bUnit storage key + manual browser |
| #7 | ✅ (deterministic) | ✅ (untracked) | required for same-session | bUnit JSInvokable + manual browser |
| #8 | ✅ (markup) | ✅ (untracked, requires seeded stream) | required for live DOM + AT | bUnit markup + manual evidence |
| #9 | ✅ (markup) | ✅ (untracked, requires seeded stream) | required for live DOM + AT | bUnit markup + manual evidence |
| #10 | ✅ | — | — | evidence index gate |
| #11 | (meta) | (meta) | — | the test mix itself |
| #12 | ✅ | — | — | source-content invariants |
| #13 | ✅ | — | — | disposition marker gate |
| #14 | ✅ | — | — | scope-boundary gate (File List) |
| #15 | ✅ | — | — | bookkeeping gate |

### Red-Phase Compliance Check

- ✅ Every test method is `[Fact(Skip = "...")]` or `[Theory(Skip = "...")]`.
- ✅ Every Skip message names the AC and the action that unmarks it.
- ✅ All assertions use `Shouldly` with `customMessage` documenting the AC contract.
- ✅ Build clean (Release, 0/0); runtime: 25 skipped, 0 failed, 0 passed in the bUnit project.
- ✅ No regressions to the 622 existing tests in the bUnit project.

## Handoff Summary

The DW5 dev should:

1. Read this checklist and the story.
2. Run `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/` to confirm the 25 scaffolds skip.
3. Per AC, perform the work the Skip message names, then remove the Skip and watch red → green.
4. Capture manual browser evidence for AC #2, #5, #6, #7, #8 live DOM, #9 live DOM under
   `_bmad-output/test-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups/`.
5. Update the deferred-work dispositions narrowly (AC #13).
6. Close bookkeeping (AC #15) at handoff.
7. Run targeted bUnit tests + record blockers per Story Task 5.

The E2E scaffolds are runnable once the E2E project is properly tracked and added to the
solution; that scope does not belong to DW5.

## Step 5: Validate & Complete

### Validation Results

- Checklist frontmatter includes `storyId`, `storyKey`, `storyFile`, `atddChecklistPath`, and deterministic generated test paths.
- Red-phase scaffold check passed: DW5 bUnit scaffolds use `[Fact(Skip = ...)]` / `[Theory(Skip = ...)]` and E2E scaffolds use `[Fact(Skip = ...)]`; no active DW5 scaffold was emitted.
- Placeholder assertion check passed: no `expect(true).toBe(true)` / equivalent placeholder pattern was found in the generated DW5 scaffolds.
- CLI session hygiene: no Playwright CLI/browser recording session was opened for this ATDD run.
- Temp artifact storage: no new random temp artifacts were required during Step 5; final workflow state is stored in this checklist.

### Commands Run

```powershell
dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release --filter "FullyQualifiedName~Dw5"
```

Result: `0 failed, 0 passed, 25 skipped, 25 total`. This is the intended ATDD red phase.

```powershell
dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release
```

Result: `0 failed, 622 passed, 25 skipped, 647 total`.

```powershell
dotnet test tests/Hexalith.EventStore.Admin.UI.E2E/Hexalith.EventStore.Admin.UI.E2E.csproj --configuration Release --filter "FullyQualifiedName~Dw5"
```

Result: restore failed with `NU1010` because `tests/Hexalith.EventStore.Admin.UI.E2E/Hexalith.EventStore.Admin.UI.E2E.csproj` references `xunit` while central package management only defines `xunit.v3`. This confirms the pre-existing E2E project blocker already documented above; the E2E source folder is also matched by `.gitignore` pattern `*.e2e`.

### Completion Summary

- Test files created/tracked by this checklist: 10 generated DW5 files (7 bUnit-side files including helper, 3 E2E-side browser scaffolds).
- Checklist output path: `_bmad-output/test-artifacts/atdd-checklist-post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md`.
- Story handoff path: `_bmad-output/implementation-artifacts/post-epic-deferred-dw5-admin-ui-runtime-follow-ups.md`.
- Key assumption: DW5 browser runtime proof remains manual or blocked until the E2E project tracking/CPM issue is handled outside this ATDD workflow.
- Next recommended workflow: `bmad-dev-story` for DW5 implementation, activating skipped scaffolds task by task after first confirming red.
