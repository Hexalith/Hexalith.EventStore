# Sprint Change Proposal — Fluent UI Blazor v4 → v5 Migration

**Date:** 2026-04-13
**Author:** Jerome
**Scope:** Major
**Status:** Approved
**Elicitation:** Pre-mortem, Self-Consistency, Red/Blue Team, Failure Mode Analysis, Critical Challenge (2026-04-13)

---

## Section 1: Issue Summary

### Problem Statement

The Hexalith.EventStore project uses Fluent UI Blazor v4.14.0 across three Blazor projects (Admin.UI, Sample.BlazorUI, design-directions prototype). V5.0.0 is available with fundamental architectural changes — migration from FAST-based Web Components v2 to Fluent UI Web Components v3 (the same rendering layer powering Microsoft 365, Teams, and Windows 11). V4 support ends November 2026 (7 months away).

### Trigger

Strategic technology upgrade driven by:

- **Proactive alignment** with Microsoft's Fluent 2 design system
- **V4 end-of-life** in 7 months (November 2026)
- **New v5 features**: FluentLayout (declarative area-based layout), DefaultValues system, FluentProviders (consolidated providers), first-class localization, FluentText typography, AI-powered MCP development companion
- **Avoiding migration debt** — the codebase is young (62 .razor files, 949 component occurrences) and easier to migrate now than after further growth

### Evidence

- V5 RC1 published February 2026; ~85% of components migrated; GA imminent
- V5 TODO (#4182): only 2/20 component migrations pending + 2/13 general items
- Official per-component migration guides exist for all 57 affected components
- Fluent UI Blazor MCP Server provides real-time migration guidance
- Comprehensive research completed 2026-04-06 (`technical-fluentui-blazor-v5-research-2026-04-06.md`)

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Status | Impact |
|------|--------|--------|
| Epic 15 (Admin Web UI — Core Dev Experience) | in-progress (12/13) | Retroactive migration of all completed UI stories. Story 15-13 (backend) unaffected |
| Epic 16 (Admin Web UI — DBA Operations) | done | Retroactive migration of Tenants, Snapshots, Backups, DeadLetters, Compaction, Consistency pages |
| Epic 17 (Admin CLI) | done | No impact — CLI has no UI |
| Epic 18 (Admin MCP Server) | done | No impact — no UI |
| Epic 19 (DAPR Infrastructure Visibility) | done | Retroactive migration of DaprActors, DaprComponents, DaprPubSub, DaprHealthHistory, DaprResiliency pages |
| Epic 20 (Advanced Debugging) | partial | Retroactive migration of BlameViewer, BisectTool, EventDebugger, CommandSandbox, CorrelationTraceMap, StateDiffViewer |

**New epic required:** Epic 21 — Fluent UI Blazor v4 → v5 Migration (10 stories)

### Artifact Conflicts

| Artifact | Conflict | Resolution |
|----------|----------|------------|
| PRD | Technology stack doesn't explicitly list Fluent UI version | Add "Fluent UI Blazor 5.x" to tech stack |
| Architecture | Tech stack table references v4.13.2 | Update to v5.0.0 |
| Architecture | Frontend row references "Blazor Fluent UI 4.x" | Update to "5.x" |
| Epics | No migration epic exists | Add Epic 21 with 10 stories |
| Sprint Status | No Epic 21 entries | Add epic and story entries |
| UX Design | Component names reference v4 (FluentNavMenu, etc.) | Component names updated in implementation |

### Technical Impact

#### Codebase Scope

| Metric | Count |
|--------|-------|
| Total .razor files affected | 62 (Admin.UI) + 11 (Sample) = 73 |
| Total Fluent UI component occurrences | 949+ |
| CSS files with v4 token variables | 5 files, ~23 occurrences |
| CSS files with `::deep` selectors | 4 files, 12 occurrences |

#### Breaking Change Impact Matrix

| V4 Component/API | V5 Replacement | Files | Severity |
|---|---|---|---|
| `FluentDesignTheme` | CSS custom properties | 3 | Critical |
| `FluentHeader` | `FluentLayoutItem Area="Header"` | 2 | Critical |
| `FluentBodyContent` | `FluentLayoutItem Area="Content"` | 2 | Critical |
| `FluentMenuProvider` | `<FluentProviders />` | 1 | Critical |
| `Directory.Packages.props` version | `5.0.0.26098` | 1 | Critical |
| `Appearance` enum | `ButtonAppearance` / `BadgeAppearance` / `LinkAppearance` | 45+ | High |
| `FluentNavMenu` / `FluentNavGroup` / `FluentNavLink` | `FluentNav` / `FluentNavCategory` / `FluentNavItem` | 2 | High |
| `IToastService.ShowSuccess()` etc. | `IToastService.ShowToastAsync()` | 9 (90+ call sites) | High |
| `FluentDialogHeader` / `FluentDialogFooter` | `FluentDialogBody` with `TitleTemplate` / `ActionTemplate` | 12 (30 instances) | High |
| `FluentTextField` / `FluentNumberField` / `FluentSearch` | `FluentTextInput` | 30 | Medium |
| `FluentAnchor` | `FluentLink` / `FluentAnchorButton` | 10 | Medium |
| `FluentSelect` | Add `TValue` type param | 11 (18 instances) | Medium |
| `FluentProgressRing` | `FluentSpinner` | 8 (28 instances) | Low |
| `FluentDataGrid` enums | `DataGridCellAlignment`, `DataGridSortDirection` | 29 | Low |
| `FluentTab Label=` | `FluentTab Header=` | 1 | Low |
| `FluentBadge ChildContent` | `FluentBadge Content=` | 8 (~20 instances) | Low |
| `FluentStack` gap defaults | `0` instead of `10px` | 18 | Low |
| CSS v4 FAST tokens | CSS v5 Fluent 2 tokens | 5 | Medium |
| `::deep` CSS selectors | Remove (scoped bundling disabled) | 4 | Low |
| `DesignThemeModes` enum | Custom `ThemeMode` enum + CSS `color-scheme` | 2 | Medium |
| `FluentToastProvider` | Keep (still exists in v5, inside `FluentProviders`) | 1 | Low |

---

## Section 3: Recommended Approach

### Selected Path: Direct Adjustment

Migrate the existing codebase in-place via a dedicated epic (Epic 21) with 10 sequenced stories.

### Rationale

1. **Migration is well-documented** — 57 per-component migration guides + MCP Server migration guardrails
2. **V5 APIs are stabilized** — RC1 with ~85% complete; risk of further breaking changes is low
3. **V4 EOL in 7 months** — migrating now is proactive vs. reactive
4. **Codebase is young** — no legacy baggage; all v4 code was written in the last 2 months
5. **Changes are mostly mechanical** — renames, enum mappings, structural reshuffling; no algorithmic changes
6. **New features simplify code** — FluentLayout replaces manual CSS layout, FluentProviders consolidates 3+ providers, DefaultValues eliminates repetitive parameter setting

### Branch Strategy (Red Team finding)

All Epic 21 work executes on a `feat/fluent-ui-v5-migration` branch. If any story introduces unrepairable regressions, the branch is abandoned and v4 continues until v5 GA.

### Rollback Criteria (Red Team finding)

If any individual story takes >3x estimated effort (i.e., exceeds a full dev session after MCP-assisted migration), halt the epic and reassess. Possible outcomes: wait for v5 GA, request upstream fix, or redesign the story approach.

### V5 GA Reconciliation (Pre-mortem finding)

When v5 GA ships, a follow-up Story 21-11 must diff RC→GA changelog and fix any breaking delta. The `5.0.0.26098` build number is not a stable release tag. This is a known follow-up, not a surprise.

### Effort Estimate: Medium

- 10 stories, each independently testable
- Most changes are find-and-replace or structural template changes
- Largest volume: Appearance enum (277 occurrences), Toast API (90+ call sites), Dialog restructure (30 instances)
- MCP Server available as real-time migration assistant

### Risk Assessment

| Risk | Level | Mitigation |
|------|-------|------------|
| V5 GA not shipped yet | Medium | RC1 APIs stabilized; v4 fallback until Nov 2026; pin package version |
| Visual regressions from WC v3 | Medium | Test each page after migration; compare screenshots |
| Missing components in v5 | Low | All components we use are confirmed migrated in v5 |
| FluentNumberField loss of typed binding | Medium | Monitor #4544 (FluentNumberInput wrapper); interim string parse |
| CSS token renames missed | Low | Systematic grep + visual testing |
| Dialog async show/hide changes | Medium | Comprehensive testing of all 30 dialog instances |

### Timeline Impact

No impact on existing sprint timeline. Epic 21 runs after current work completes. Story 15-13 (stream activity tracker writer) is a backend story and can proceed independently.

---

## Section 4: Detailed Change Proposals

> **Party Mode Review Applied (2026-04-13):** Recommendations from Winston (Architect), Amelia (Developer), Bob (Scrum Master), Murat (Test Architect), and Sally (UX Designer) incorporated below. Changes: added Story 21-0 (bUnit smoke tests), merged Stories 21-2+21-3 (Layout+Navigation), split Story 21-4 into 21-4a/21-4b (ButtonAppearance/BadgeAppearance), added NumericInput wrapper to 21-5, added localStorage theme persistence to 21-2, added per-story visual verification checklist, added "zero v4 references" success criterion.

### Story 21-0: bUnit Smoke Tests Before Migration (Baseline)

**Rationale (Murat):** No existing UI tests cover Blazor rendering. Adding baseline bUnit render tests before migration provides a before/after regression signal.

**Changes:**
- Add `Hexalith.EventStore.Admin.UI.Tests` bUnit test project (if not already present)
- Add 5-6 basic render-and-verify tests: MainLayout, NavMenu, Index page, a representative data page (Streams or Commands), a dialog-heavy page (Tenants), StatCard component
- Tests verify components render without exceptions and contain expected structural elements
- Run and record baseline pass before any v5 changes

**Visual verification:** N/A (test infrastructure story)

### Story 21-1: Package Version + csproj Infrastructure

**Changes:**
- `Directory.Packages.props`: `4.14.0` → `5.0.0.26098` for both `Components` and `Icons` packages
- `Admin.UI.csproj`: Add `<DisableScopedCssBundling>true</DisableScopedCssBundling>` and `<ScopedCssEnabled>false</ScopedCssEnabled>`
- `Sample.BlazorUI.csproj`: Same scoped CSS properties
- `design-directions-prototype.csproj`: Remove `VersionOverride="4.13.2"`
- Verify `dotnet restore` succeeds
- Verify `dotnet build Hexalith.EventStore.slnx` passes for ALL projects including samples (Pre-mortem: don't defer sample breakage to 21-10)
- Verify zero v4 package references remain in any .csproj or .props file

**Visual verification:** `[ ] dotnet build succeeds for entire solution` (pages won't render yet — expected)

### Story 21-2: Layout + Navigation Foundation

**Rationale for merge (Amelia, Bob):** Layout and navigation are tightly coupled — `FluentLayoutItem Area="LayoutArea.Menu"` hosts the `FluentNav`. Cannot test layout without navigation done.

**Changes:**
- `Admin.UI/Components/App.razor`: Add `<FluentProviders>` wrapping `<ErrorBoundary>` + Routes; keep `FluentToastProvider` inside `FluentProviders`; update CSS variable names in body style; `Appearance.Accent` → `ButtonAppearance.Primary`
- `Admin.UI/Layout/MainLayout.razor`: Remove `FluentDesignTheme` wrapper; remove `FluentMenuProvider`; replace `FluentHeader` → `FluentLayoutItem Area="LayoutArea.Header"`; replace `FluentBodyContent` → `FluentLayoutItem Area="LayoutArea.Content"` + `FluentLayoutItem Area="LayoutArea.Menu"`; remove `OnThemeModeChanged` method; remove `DesignThemeModes` usage
- `Admin.UI/Services/ThemeState.cs`: Replace `DesignThemeModes` enum with custom `ThemeMode { System, Light, Dark }` enum; implement CSS `color-scheme` toggle via JS interop + localStorage persistence
- `Admin.UI/Components/ThemeToggle.razor`: Update to use new `ThemeMode` enum
- `Admin.UI/Layout/NavMenu.razor`: `FluentNavMenu` → `FluentNav`; `FluentNavLink` → `FluentNavItem` (×17); `FluentNavGroup` → `FluentNavCategory`; `Width` int → CSS string; remove `Title`, `@bind-Expanded`; `Icon=` → `IconRest=`; remove `IconColor="Color.Accent"`; remove `_expanded` field
- `Sample.BlazorUI/Components/App.razor`: Remove `FluentDesignTheme`; add `<FluentProviders>` wrapping Routes
- `Sample.BlazorUI/Layout/MainLayout.razor`: Replace `FluentHeader`/`FluentBodyContent` with `FluentLayoutItem`; `FluentNavMenu` → `FluentNav`; `FluentNavLink` → `FluentNavItem`

**Acceptance Criteria (Sally, Winston, Failure Mode):**
- Given the user selects dark mode, When they reload the page, Then dark mode persists (localStorage)
- Given a narrow viewport (<960px), Then FluentLayout collapses the navigation panel
- Given FluentProviders wraps the component tree, When any page with FluentButton renders, Then no "LibraryConfiguration not found" or cascading value errors occur (Failure Mode: FluentProviders cascading risk)

**Visual verification:** `[ ] Light mode verified [ ] Dark mode verified [ ] Navigation expands/collapses [ ] Theme persists across reload`

### Story 21-3: Appearance Enum — ButtonAppearance (~215 occurrences, ~42 files)

**Changes on FluentButton only:**
- `Appearance.Accent` → `ButtonAppearance.Primary`
- `Appearance.Outline` → `ButtonAppearance.Outline`
- `Appearance.Lightweight` → `ButtonAppearance.Transparent`
- `Appearance.Stealth` → `ButtonAppearance.Subtle`
- `Appearance.Neutral` → `ButtonAppearance.Default`
- `Appearance.Hypertext` on FluentButton (if any) → `ButtonAppearance.Default`
- **IMPORTANT (Failure Mode):** Grep BOTH `*.razor` AND `*.cs` files — Appearance enum is used in code-behind status-mapping methods (e.g., Backups.razor, Compaction.razor tuple returns)

**Visual verification:** `[ ] Light mode verified [ ] Dark mode verified [ ] Spot-check 5 representative pages: Index, Streams, Tenants, Backups, Consistency`

### Story 21-4: Appearance Enum — BadgeAppearance + LinkAppearance + Badge Content (~60 occurrences, ~10 files)

**Changes on FluentBadge:**
- `Appearance.Accent` → `BadgeAppearance.Filled`; `Appearance.Neutral` → `BadgeAppearance.Filled`; `Appearance.Lightweight` → `BadgeAppearance.Ghost`
- `Color.Error` → `BadgeColor.Danger`; `Color.Warning` → `BadgeColor.Warning`; `Color.Success` → `BadgeColor.Success`
- `ChildContent` text → `Content` parameter (e.g. `<FluentBadge>Full</FluentBadge>` → `<FluentBadge Content="Full" />`)
- Badge status tuples in Backups.razor/Compaction.razor: update return types from `Appearance` to `BadgeAppearance`

**Changes on FluentAnchor → FluentLink / FluentAnchorButton (10 files):**
- `FluentAnchor` with `Appearance.Hypertext` → `FluentLink` with `Appearance="LinkAppearance.Default"`
- `FluentAnchor` with `Appearance.Accent` (EmptyState) → `FluentAnchorButton` with `Appearance="ButtonAppearance.Primary"`
- `FluentAnchor` with `Appearance.Stealth` (IssueBanner) → `FluentLink` with `Appearance="LinkAppearance.Subtle"`
- `Target="_blank"` → `Target="LinkTarget.Blank"` (typed enum)

**Additional AC (Failure Mode):** Verify badges with interpolated string content render correctly — e.g., `Content="@($"Events ({count})")"` must produce the same output as v4's `<FluentBadge>Events (@count)</FluentBadge>`

**Visual verification:** `[ ] Light mode verified [ ] Dark mode verified [ ] Badge colors correct on Backups, DaprResiliency, TypeDetailPanel [ ] Badges with dynamic content render correctly [ ] Links work on DaprActors, Index, EmptyState`

### Story 21-5: Component Renames

**Changes:**
- `FluentTextField` → `FluentTextInput` (13 files); `TextFieldType` → `TextInputType`; `Maxlength` → `MaxLength`
- `FluentNumberField` → `FluentTextInput` with `TextInputType="TextInputType.Number"` (6 files); create shared `NumericInput` wrapper component (`Admin.UI/Components/Shared/NumericInput.razor`) that encapsulates string→numeric parsing via `int.TryParse`/`decimal.TryParse` with explicit error feedback (`MessageState`), reused across all 6 files (Winston: don't scatter parse logic; Pre-mortem: browser inconsistencies with `type="number"` require explicit validation)
- `FluentSearch` → `FluentTextInput` with `<StartTemplate>` search icon (11 files)
- `FluentProgressRing` → `FluentSpinner` (8 files, 28 instances); `Style="width:16px"` → `Size="SpinnerSize.Small"`
- `FluentSelect`: Add `TValue="string"` to all 18 instances across 11 files

**Visual verification:** `[ ] Light mode verified [ ] Dark mode verified [ ] Search fields show search icon [ ] Number inputs accept/reject non-numeric input [ ] Spinners render at correct sizes`

### Story 21-6: Dialog Restructure

**Risk note (Amelia):** This is the highest-regression-risk story. The `@bind-Hidden` → `ShowAsync/HideAsync` change converts synchronous show/hide to async. Some toggle methods are `void` event handlers that must become `async Task`. This is code-behind refactoring, not just template restructuring.

**Changes across 12 files (30 dialog instances):**
- Remove `TrapFocus="true"`, `aria-modal="true"`, `PreventDismiss`, `OnDismiss`
- Replace `@bind-Hidden="_flag"` with `@ref="_dialog"` + `ShowAsync()`/`HideAsync()`
- Replace `<FluentDialogHeader>Title</FluentDialogHeader>` with `<TitleTemplate>Title</TitleTemplate>` inside `<FluentDialogBody>`
- Replace `<FluentDialogFooter>buttons</FluentDialogFooter>` with `<ActionTemplate>buttons</ActionTemplate>` inside `<FluentDialogBody>`
- Make `<FluentDialogBody>` the single structural child wrapping title + content + actions
- Convert boolean show/hide fields to `FluentDialog?` ref fields
- Update all show/hide callsites to async; convert `void` handlers to `async Task`

**Additional AC (Failure Mode, Pre-mortem):**
- All `ShowAsync()`/`HideAsync()` calls use null-conditional or null guard — never call on unrendered `@ref`
- Multi-dialog pages (Tenants 6, Backups 6): test rapid open→close→reopen sequences to verify no race conditions
- Disable trigger buttons during async dialog transitions where applicable

**Test protocol (Murat):** Open and close each of the 30 dialogs. Verify button actions fire correctly. Pay special attention to multi-dialog pages (Tenants: 6, Backups: 6). Test rapid open/close/reopen on Tenants and Backups.

**Visual verification:** `[ ] Light mode verified [ ] Dark mode verified [ ] All 30 dialogs open/close [ ] Dialog button actions fire [ ] CommandPalette Ctrl+K shortcut still works [ ] Rapid open/close on Tenants page stable`

### Story 21-7: Toast API Update

**Changes:**
- Create `Admin.UI/Services/ToastServiceExtensions.cs` with `ShowSuccessAsync`, `ShowErrorAsync`, `ShowWarningAsync`, `ShowInfoAsync` extension methods wrapping `ShowToastAsync` + `ToastIntent`
- Update 90+ call sites across 9 files: `.ShowSuccess(` → `await .ShowSuccessAsync(`; `.ShowError(` → `await .ShowErrorAsync(`; etc.
- Ensure calling methods are `async Task` (most already are)
- Optional enhancement (Sally): enrich error toasts on "Admin service unavailable" call sites with `QuickAction1 = "Retry"` where a retry action exists

**Visual verification:** `[ ] Light mode verified [ ] Dark mode verified [ ] Trigger a success toast (e.g. create tenant) [ ] Trigger an error toast (e.g. disconnect admin API) [ ] Toast auto-dismisses after timeout`

### Story 21-8: CSS Token Migration

**Changes:**
- `app.css`: Replace 13 v4 FAST token references with v5 Fluent 2 tokens (e.g. `--neutral-layer-1` → `--colorNeutralBackground1`)
- `Health.razor.css`: Replace 3 token references
- `ActivityChart.razor.css`: Replace 2 token references
- `JsonViewer.razor.css`: Replace 5 token references
- Remove `::deep` selectors from 4 files (12 occurrences): `StateDiffViewer.razor.css`, `TypeCatalog.razor.css`, `Health.razor.css`, `TypeDetailPanel.razor.css`
- Remove or update scoped CSS bundle link in App.razor

**Pre-step (Red Team, Pre-mortem):** Before renaming any CSS variables, open browser DevTools on a running v5 page and inspect the actual CSS custom property names emitted by v5. Verify the v4→v5 mapping table against reality. Do NOT rely solely on inferred naming patterns.

**Risk note (Winston):** V4 FAST tokens and v5 Fluent 2 tokens may have different computed values, especially for dark mode luminance. Compare visual output carefully.

**Additional AC (Pre-mortem):** Run browser DevTools Accessibility audit on 3 representative pages (Index, Streams, Tenants) in dark mode to verify WCAG AA contrast ratios are maintained after token rename.

**Visual verification:** `[ ] Light mode verified — every page [ ] Dark mode verified — every page [ ] DevTools Accessibility audit passes on Index, Streams, Tenants (dark mode) [ ] ActivityChart renders correctly [ ] JsonViewer syntax highlighting visible [ ] StateDiffViewer diff colors distinguishable`

### Story 21-9: DataGrid + Remaining Component Enum Renames

**Changes:**
- `Align.Start`/`.End`/`.Center` → `DataGridCellAlignment.Start`/`.End`/`.Center` (~5 occurrences)
- `SortDirection.Ascending`/`.Descending` → `DataGridSortDirection.Ascending`/`.Descending` (~10 occurrences)
- `FluentTab Label=` → `FluentTab Header=` (3 instances in TypeCatalog.razor)
- **FluentOverlay (Self-Consistency gap):** Verify actual usage in Admin.UI — audit reported 8 files but grep returned 0. If FluentOverlay IS used: migrate `Dismissable` → `CloseMode`, `Opacity` double→int, remove `Transparent`/`OnClose`/`Alignment`/`PreventScroll`. If NOT used: confirm and close gap.
- Verify `FluentStack` gap behavior; add explicit gap if needed or configure via `DefaultValues`
- Verify `FluentSkeleton` `SkeletonShape` enum still exists
- Verify `FluentIcon` default color change is acceptable (currentColor vs. accent)

**Visual verification:** `[ ] Light mode verified [ ] Dark mode verified [ ] DataGrid sorting works [ ] TypeCatalog tabs render with correct labels [ ] Column alignment preserved`

### Story 21-10: Sample BlazorUI + Design Directions Alignment

**Changes:**
- `Sample.BlazorUI/Layout/MainLayout.razor`: Full layout restructure (FluentLayoutItem, FluentNav) — already partially done in 21-2
- Sample component files (6): Appearance enum updates, any other applicable renames
- Design-directions prototype: Remove VersionOverride, apply same v5 changes or mark as archived
- End-to-end visual verification of all Sample UI pages

**Visual verification:** `[ ] Light mode verified [ ] Dark mode verified [ ] All 3 refresh pattern pages render [ ] Counter increment/decrement works [ ] Design directions prototype builds or is archived`

---

## Section 5: Implementation Handoff

### Change Scope: Major

This is a comprehensive technology migration affecting 73 .razor files, 5 CSS files, 3 .csproj files, 1 .props file, and 4 planning artifacts.

### Handoff Plan

| Role | Responsibility |
|------|---------------|
| **Developer (Amelia)** | Execute stories 21-1 through 21-10 sequentially; run visual verification after each story |
| **Scrum Master (Bob)** | Update sprint-status.yaml; track Epic 21 progress; create stories via create-story workflow |
| **QA (Quinn)** | Visual regression testing after stories 21-2 (layout), 21-6 (dialog), 21-8 (CSS); verify all Admin UI pages render correctly |

### Success Criteria

- `dotnet build Hexalith.EventStore.slnx` succeeds with zero warnings from Fluent UI obsolete APIs
- **Zero v4 package references** remain in any .csproj or .props file (Winston)
- All Admin UI pages render correctly with Fluent 2 design system aesthetics — **verified in both light and dark mode** (Murat)
- Dark mode / light mode toggle works via CSS custom properties **and persists across page reload via localStorage** (Sally)
- All 30 dialog instances open/close correctly via ShowAsync/HideAsync
- All toast notifications display correctly
- Navigation menu renders and functions with FluentNav components
- Sample BlazorUI runs and displays all 3 refresh patterns correctly
- No v4 `Appearance` enum references remain in codebase
- No v4 CSS FAST token references remain in stylesheets
- No `::deep` selectors remain in component CSS files
- bUnit baseline smoke tests pass against v5 components (Murat)

### Recommended Execution Order

Stories are sequenced by dependency:
1. **21-0** (bUnit smoke tests) — establish baseline before any changes
2. **21-1** (packages) — everything depends on this
3. **21-2** (layout + navigation) — structural foundation (merged: layout + nav are one deployable unit)
4. **21-3** (ButtonAppearance) — high volume, independent
5. **21-4** (BadgeAppearance + LinkAppearance) — complementary to 21-3 but different API surface
6. **21-5** (component renames) — independent
7. **21-6** (dialog) — independent, **highest regression risk** — allocate its own session
8. **21-7** (toast) — independent
9. **21-8** (CSS) — independent, **requires full visual regression sweep**
10. **21-9** (DataGrid/remaining) — cleanup
11. **21-10** (Sample) — last, applies all patterns

Stories 21-3 through 21-9 can be parallelized if multiple developers are available.

---

## Checklist Status

| Section | Status |
|---------|--------|
| 1.1 Triggering story | [x] Done |
| 1.2 Core problem | [x] Done |
| 1.3 Evidence | [x] Done |
| 2.1 Current epic | [x] Done |
| 2.2 Epic-level changes | [x] Done |
| 2.3 Remaining epics | [x] Done |
| 2.4 New epics needed | [x] Done |
| 2.5 Epic priority | [x] Done |
| 3.1 PRD conflicts | [x] Done |
| 3.2 Architecture conflicts | [!] Action-needed → resolved in Proposal 14b |
| 3.3 UI/UX conflicts | [!] Action-needed → resolved in implementation |
| 3.4 Other artifacts | [!] Action-needed → resolved in Proposals 12, 14 |
| 4.1 Direct Adjustment | [x] Viable — SELECTED |
| 4.2 Rollback | [N/A] Skip |
| 4.3 MVP Review | [N/A] Skip |
| 4.4 Path selected | [x] Done |
| 5.1 Issue summary | [x] Done |
| 5.2 Impact analysis | [x] Done |
| 5.3 Recommended path | [x] Done |
| 5.4 MVP impact | [x] Done — MVP unaffected |
| 5.5 Handoff plan | [x] Done |

---

## References

- [Fluent UI Blazor v5 RC1 Announcement](https://baaijte.net/blog/microsoft-fluentui-aspnetcore.components-50-rc1/)
- [V5 Preview Site & Migration Guide](https://fluentui-blazor-v5.azurewebsites.net/MigrationV5)
- [GitHub dev-v5 Branch](https://github.com/microsoft/fluentui-blazor/tree/dev-v5)
- [V5 TODO List (#4182)](https://github.com/microsoft/fluentui-blazor/issues/4182)
- [MCP Server](https://dvoituron.com/2026/02/20/fluentui-blazor-mcp-server/)
- [Internal Research: technical-fluentui-blazor-v5-research-2026-04-06.md](./research/technical-fluentui-blazor-v5-research-2026-04-06.md)
