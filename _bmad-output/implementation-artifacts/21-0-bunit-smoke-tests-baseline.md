# Story 21.0: bUnit Smoke Tests Before Migration (Baseline)

Status: done

## Story

As a developer preparing for the Fluent UI Blazor v4 → v5 migration,
I want baseline bUnit render tests for all key UI components,
so that I have a before/after regression signal to verify the migration doesn't break rendering.

## Acceptance Criteria

1. **Given** the `Hexalith.EventStore.Admin.UI.Tests` project already exists,
   **When** the developer reviews the test coverage,
   **Then** dedicated render tests exist for all 7 target components: MainLayout, NavMenu, Index page, Streams page (representative data page), Tenants page (dialog-heavy page), StatCard component, and EmptyState component.

2. **Given** NavMenu has no dedicated test file,
   **When** `NavMenuTests.cs` is created,
   **Then** it verifies NavMenu renders without exceptions, contains all 10+ navigation links (Home, Commands, Events, Streams, Projections, Health, Services, Tenants, Type Catalog, Settings), **and asserts v4-specific structural element names** (`fluent-nav-menu`, `fluent-nav-link`, `fluent-nav-group`) are present in the rendered markup — these are the primary migration regression markers.

3. **Given** StatCard has no dedicated test file,
   **When** `StatCardTests.cs` is created,
   **Then** it verifies StatCard renders with label, value, severity-based inline color style (e.g., `color: var(--hexalith-status-success)` via the `_severityColor` property), and loading skeleton state.

4. **Given** EmptyState has no dedicated test file,
   **When** `EmptyStateTests.cs` is created,
   **Then** it verifies EmptyState renders title, description, and when ActionLabel+ActionHref are provided, renders a `FluentAnchor` with `Appearance.Accent` — a direct v5 migration target (`FluentAnchor` → `FluentAnchorButton`).

5. **Given** the full Admin.UI.Tests suite,
   **When** `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/` runs,
   **Then** all tests pass (zero failures) on the current v4.14.0 codebase, establishing the green baseline. If pre-existing tests fail (not tests created in this story), document the failures in the Dev Agent Record and proceed — the baseline records the actual state, not an idealized state. Fix pre-existing failures only if they are trivially fixable (e.g., flaky timeout — increase timeout).

6. **Given** all baseline tests pass,
   **When** the developer completes this story,
   **Then** the following are recorded in the Dev Agent Record section: total test count, pass count, fail count, skip count, the exact `dotnet test` summary output line, and the Fluent UI package version (4.14.0).

## Tasks / Subtasks

- [x] Task 1: Create NavMenu render tests (AC: 2)
  - [x] 1.1: Create `tests/Hexalith.EventStore.Admin.UI.Tests/Layout/NavMenuTests.cs`
  - [x] 1.2: Test: NavMenu renders without exceptions when given default parameters
  - [x] 1.3: Test: NavMenu contains expected navigation labels from ALL sections — use `WaitForAssertion` since topology section loads async via `TopologyCacheService`. Assert at minimum: **Core:** Home, Commands, Events, Streams, Tenants, Health; **DBA:** Storage, Snapshots, Backups; **DAPR:** Components, Actors; **Debug:** Blame Viewer, Event Debugger; **Admin:** Settings
  - [x] 1.4: Test: NavMenu renders v4 structural elements (`fluent-nav-menu`, `fluent-nav-link`, `fluent-nav-group`) — these are the primary migration regression markers
  - [x] 1.5: Test: NavMenu Settings link hidden for non-admin role
- [x] Task 2: Create StatCard component render tests (AC: 3)
  - [x] 2.1: Create `tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/StatCardTests.cs`
  - [x] 2.2: Test: StatCard renders label and value correctly
  - [x] 2.3: Test: StatCard applies severity-based inline color style — assert markup contains `color: var(--hexalith-status-success)` for "success", `var(--hexalith-status-warning)` for "warning", `var(--hexalith-status-error)` for "error", `var(--neutral-foreground-rest)` for "neutral". Also assert `fluent-card` tag is rendered (migration regression marker for the card container).
  - [x] 2.4: Test: StatCard shows skeleton/loading state when `IsLoading=true` (renders `SkeletonCard` instead of value)
  - [x] 2.5: Test: StatCard renders accessibility live region (`aria-live="polite"`)
- [x] Task 3: Create EmptyState component render tests (AC: 4)
  - [x] 3.1: Create `tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/EmptyStateTests.cs`
  - [x] 3.2: Test: EmptyState renders title and description
  - [x] 3.3: Test: EmptyState renders `FluentAnchor` with `Appearance.Accent` when ActionLabel+ActionHref provided — migration regression marker (`FluentAnchor` → `FluentAnchorButton` in v5)
  - [x] 3.4: Test: EmptyState hides action link when ActionLabel/ActionHref not provided
- [x] Task 4: Run full suite and record baseline (AC: 5, 6)
  - [x] 4.1: Run `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/` and verify zero failures
  - [x] 4.2: Record in Dev Agent Record: total test count, pass count, fail count, skip count, exact `dotnet test` summary line, and Fluent UI package version (4.14.0)

### Review Findings

- [x] [Review][Dismissed] Missing spec-required nav link assertions: Components, Actors, Blame Viewer, Event Debugger — spec Task 1.3 link list was inaccurate; NavMenu.razor has no separate DAPR sub-links or Debug section links. Test correctly reflects actual component.
- [x] [Review][Patch] EmptyState action link test doesn't assert ActionHref URL is rendered [EmptyStateTests.cs:38] — FIXED: added `markup.ShouldContain("/streams/new")` assertion
- [x] [Review][Defer] `">99<"` fragile assertion pattern in skeleton loading test [StatCardTests.cs:55] — deferred, beyond baseline scope
- [x] [Review][Defer] Operator role not tested for Settings visibility (>= boundary) [NavMenuTests.cs:78-93] — deferred, beyond baseline scope
- [x] [Review][Defer] No test for partial ActionLabel/ActionHref (only one provided) [EmptyStateTests.cs] — deferred, beyond baseline scope
- [x] [Review][Defer] No test for Icon/ChildContent RenderFragment parameters [EmptyStateTests.cs] — deferred, beyond baseline scope
- [x] [Review][Defer] No test for Subtitle/Title (tooltip) parameters on StatCard [StatCardTests.cs] — deferred, beyond baseline scope
- [x] [Review][Defer] No test for unknown/default Severity fallback values [StatCardTests.cs] — deferred, beyond baseline scope
- [x] [Review][Defer] No test for delayed aria-live announcement mechanism [StatCardTests.cs] — deferred, beyond baseline scope
- [x] [Review][Defer] No Dispose/lifecycle tests for NavMenu or StatCard — deferred, beyond baseline scope

## Dev Notes

### Context: This Is a Pre-Migration Baseline Story

This story is the first in Epic 21 (Fluent UI Blazor v4 → v5 Migration). Its purpose is to establish a green test baseline **before any v5 changes**. The tests created here will be run again after each subsequent migration story (21-1 through 21-10) to detect regressions.

### Test Project Already Exists

The `Hexalith.EventStore.Admin.UI.Tests` project is already fully set up with:
- **bUnit 2.7.2** (centralized in `Directory.Packages.props` line 66)
- **xUnit v3**, **Shouldly**, **NSubstitute**, **coverlet.collector**
- **75+ existing test files** covering pages, components, services, layout
- **AdminUITestContext base class** at `tests/Hexalith.EventStore.Admin.UI.Tests/AdminUITestContext.cs` — provides FluentUI component registration, JSInterop mocks, authentication state, mock API clients, and SignalR test client

### What's Missing (Must Create)

1. **NavMenu render tests** — No `NavMenuTests.cs` exists. NavMenu is critical for migration because:
   - `FluentNavMenu` → `FluentNav` in v5
   - `FluentNavLink` → `FluentNavItem` in v5
   - `FluentNavGroup` → `FluentNavCategory` in v5
   - `Width` int → CSS string
   - `Icon=` → `IconRest=`

2. **StatCard component tests** — No dedicated `StatCardTests.cs` exists. StatCard is used across Index, Commands, Tenants, and multiple other pages. Testing it in isolation provides a clean regression signal. **Important:** Severity is applied via inline `color` style (e.g., `color: var(--hexalith-status-success)`), NOT via CSS class. Assert on inline style content, not class names.

3. **EmptyState component tests** — No dedicated `EmptyStateTests.cs` exists. EmptyState uses `FluentAnchor` with `Appearance.Accent` (line 16 of `EmptyState.razor`), which is a direct v5 migration target: `FluentAnchor` → `FluentAnchorButton` with `ButtonAppearance.Primary`. Testing it provides a clean regression marker for Story 21-4.

### What Already Exists (Verify Passes)

| Component | Test File | Tests |
|-----------|-----------|-------|
| MainLayout | `Layout/MainLayoutTests.cs` | 5 tests: renders layout, skip link, nav menu, main content, breadcrumb |
| Index page | `Pages/IndexPageTests.cs` | 6 tests: stat cards, skeleton, API error, empty state, stale data, severity |
| Streams page | `Pages/StreamsPageTests.cs` | 5 tests: grid columns, status badges, empty state, pagination, truncation |
| Commands page | `Pages/CommandsPageTests.cs` | Multiple tests: grid, status, stat cards |
| Tenants page | `Pages/TenantsPageTests.cs` | 20+ tests: stat cards, grid, dialogs, filtering, role-based visibility |

### Test Patterns to Follow

All tests inherit from `AdminUITestContext`. Follow the established pattern:

```csharp
public class NavMenuTests : AdminUITestContext
{
    [Fact]
    public void NavMenu_RendersAllNavigationLinks()
    {
        IRenderedComponent<NavMenu> cut = Render<NavMenu>(
            parameters => parameters
                .Add(p => p.Width, 220)
                .Add(p => p.UserRole, AdminRole.Admin));
        
        // Text assertions — verify link labels
        cut.Markup.ShouldContain("Home");
        cut.Markup.ShouldContain("Commands");
        // ... more label assertions

        // Structural assertions — verify v4 web component HTML tags (migration regression markers)
        // bUnit + AddFluentUIComponents() renders FluentUI as kebab-case web component tags
        cut.Markup.ShouldContain("fluent-nav-menu");
        cut.Markup.ShouldContain("fluent-nav-link");
    }
}
```

**Key conventions:**
- `Render<T>()` from `BunitContext` (inherited via `AdminUITestContext`)
- `ShouldContain()` / `ShouldNotContain()` from Shouldly for markup assertions
- `cut.WaitForAssertion()` with `TimeSpan.FromSeconds(5)` for async components
- `NSubstitute` for mocking services (`Substitute.For<T>()`)
- `AdminUITestContext` already registers: FluentUI components, JSInterop, auth state (Admin role), mock `AdminStreamApiClient`, `DashboardRefreshService`, `TopologyCacheService`, `ViewportService`, `TestSignalRClient`, and configuration

**Critical: bUnit renders FluentUI as web component HTML tags in kebab-case.** `AddFluentUIComponents()` registers the Fluent UI library which uses `[HtmlTag]` attributes to emit web component tags. The rendered markup contains `<fluent-nav-menu>`, `<fluent-nav-link>`, `<fluent-anchor>`, `<fluent-button>`, `<fluent-card>`, etc. — NOT PascalCase Razor component names. This is confirmed by existing tests: `TenantsPageTests.cs` line 181 asserts `fluent-button`. All structural assertions in this story MUST use kebab-case HTML tag names.

### NavMenu Component Details

**Location:** `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor`

**Parameters:**
- `Width` (int, default 220) — sidebar width
- `UserRole` (AdminRole enum) — controls Settings visibility

**Injected services (already registered in AdminUITestContext):**
- `TopologyCacheService` — loads tenant/domain topology
- `DashboardRefreshService` — refresh signals
- `NavigationManager` — navigation

**Expected navigation links (from MainLayoutTests.cs and NavMenu.razor):**
- Home, Commands, Events, Streams, Projections, Health, Services, Tenants, Type Catalog, Settings (admin-only)
- DAPR section: Components, Actors, Pub/Sub, Resiliency, Health History
- Debug section: Blame Viewer, Bisect Tool, Event Debugger, Command Sandbox, Correlation Trace Map

**Role-based rendering:** Settings link only visible when `UserRole >= AdminRole.Admin`

**Async topology loading:** NavMenu calls `TopologyCacheService` in `OnInitializedAsync` to load tenant/domain topology (lines 30-58). The `AdminUITestContext` returns empty topology by default. Use `WaitForAssertion` for any assertions that depend on the navigation section being fully rendered.

### StatCard Component Details

**Location:** `src/Hexalith.EventStore.Admin.UI/Components/Shared/StatCard.razor`

**Parameters:**
- `Label` (string) — card title
- `Value` (string) — displayed metric value
- `Severity` ("success" | "warning" | "error" | "neutral") — drives inline color style via `_severityColor` property (NOT a CSS class)
- `Title` (string) — optional tooltip
- `Subtitle` (string) — secondary text
- `IsLoading` (bool) — shows skeleton placeholder

**Accessibility:** Uses `aria-live` region for value announcements with 5-second debounce.

### EmptyState Component Details

**Location:** `src/Hexalith.EventStore.Admin.UI/Components/Shared/EmptyState.razor`

**Parameters:**
- `ChildContent` (RenderFragment?) — custom icon content
- `Icon` (RenderFragment?) — icon slot
- `Title` (string) — heading text
- `Description` (string) — description text
- `ActionLabel` (string?) — link text (renders FluentAnchor when both Label+Href provided)
- `ActionHref` (string?) — link target

**Migration-sensitive element:** Line 16 uses `FluentAnchor Href="@ActionHref" Appearance="Appearance.Accent"` — in v5 this becomes `FluentAnchorButton` with `ButtonAppearance.Primary`. This is the exact regression the test must catch.

### Project Structure Notes

- All test files use file-scoped namespaces (`namespace X.Y.Z;`)
- Test file location mirrors source: `Layout/` → `Layout/`, `Components/Shared/` → `Components/Shared/`
- No separate `_Imports.razor` in test project — use C# `using` directives
- `GlobalUsings.cs` already imports: `Shouldly`, `Xunit`, `AdminUITestContext`, `PagedResult<T>`, `AdminUI.Services`
- Project uses `Microsoft.NET.Sdk.Razor` SDK (required for bUnit Razor component testing)

### Architecture Compliance

- **Test Tier:** This is a Tier 1 test (no external dependencies). Runs in CI on every PR.
- **Test command:** `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/`
- **Warnings as errors:** Enabled — all new test code must compile clean.
- **Naming:** `{ComponentName}Tests` class, `{Component}_{Scenario}_{Expectation}()` method pattern (match existing).
- **No new packages needed** — bUnit 2.7.2, xUnit v3, Shouldly, NSubstitute already in `Directory.Packages.props`.

### Library/Framework Requirements

| Package | Version | Source |
|---------|---------|--------|
| bunit | 2.7.2 | `Directory.Packages.props` line 66 |
| Microsoft.FluentUI.AspNetCore.Components | 4.14.0 | `Directory.Packages.props` lines 45-46 |
| xunit.v3 | (centralized) | `Directory.Packages.props` |
| Shouldly | (centralized) | `Directory.Packages.props` |
| NSubstitute | (centralized) | `Directory.Packages.props` |

**DO NOT** add any new package references. All required packages are already present.

### File Structure Requirements

```
tests/Hexalith.EventStore.Admin.UI.Tests/
  Layout/
    MainLayoutTests.cs         ← EXISTS (verify passes)
    NavMenuTests.cs            ← CREATE
  Components/
    Shared/
      StatCardTests.cs         ← CREATE
      EmptyStateTests.cs       ← CREATE
  Pages/
    IndexPageTests.cs          ← EXISTS (verify passes)
    StreamsPageTests.cs        ← EXISTS (verify passes)
    TenantsPageTests.cs        ← EXISTS (verify passes)
```

### Testing Requirements

- All new tests must follow the `AdminUITestContext` base class pattern
- All new tests must use Shouldly assertions (not `Assert.Equal`)
- Tests must verify structural rendering (markup contains expected elements), not pixel-perfect output
- Tests must not depend on any external services (Tier 1 — no Docker, no DAPR)
- Run the full suite with `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/` and record results

### Git Intelligence

Recent commits show active development on Epic 15 (Admin UI) and planning for Epic 21. The codebase is stable on v4.14.0. Most recent UI test fix was `dd6445a fix(ci): use dapr/setup-dapr action in release workflow and fix flaky UI test` — be aware of potential flaky async tests. Use `WaitForAssertion` with reasonable timeouts.

### References

- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-04-13-fluent-ui-v5-migration.md#Story 21-0]
- [Source: tests/Hexalith.EventStore.Admin.UI.Tests/AdminUITestContext.cs] — base test context
- [Source: tests/Hexalith.EventStore.Admin.UI.Tests/Layout/MainLayoutTests.cs] — pattern reference
- [Source: tests/Hexalith.EventStore.Admin.UI.Tests/Pages/IndexPageTests.cs] — page test pattern
- [Source: tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TenantsPageTests.cs] — dialog test pattern
- [Source: src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor] — NavMenu source
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/StatCard.razor] — StatCard source
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/EmptyState.razor] — EmptyState source (FluentAnchor migration target)
- [Source: tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TenantsPageTests.cs#L181] — evidence that bUnit renders FluentUI as kebab-case tags (`fluent-button`)

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- StatCard_ShowsSkeletonWhenLoading initial assertion used `ShouldNotContain("Loading Metric")` which failed because the label appears in the `aria-label` attribute of `<fluent-card>`. Fixed by asserting value `>99<` is not present instead.

### Completion Notes List

- **Task 1 (NavMenu):** Created `NavMenuTests.cs` with 4 tests: renders without exception, contains all expected navigation labels (Core, DBA, DAPR, Admin), renders v4 structural elements (`fluent-nav-menu`, `fluent-nav-link`, `fluent-nav-group`), Settings hidden for ReadOnly role. All 4 tests pass.
- **Task 2 (StatCard):** Created `StatCardTests.cs` with 7 tests (4 via Theory): renders label/value, applies severity-based inline color for success/warning/error/neutral with `fluent-card` tag assertion, shows skeleton when loading, renders `aria-live="polite"` accessibility region. All 7 tests pass.
- **Task 3 (EmptyState):** Created `EmptyStateTests.cs` with 3 tests: renders title/description, renders `fluent-anchor` with `appearance="accent"` when ActionLabel+ActionHref provided (migration marker), hides action link when not provided. All 3 tests pass.
- **Task 4 (Baseline):** Full suite run — 592 tests, 592 passed, 0 failed, 0 skipped.

### Baseline Test Results (AC: 6)

- **Total tests:** 592
- **Passed:** 592
- **Failed:** 0
- **Skipped:** 0
- **dotnet test summary:** `Nombre total de tests : 592 — Réussi(s) : 592 — Durée totale : 18,6154 Secondes`
- **Fluent UI package version:** Microsoft.FluentUI.AspNetCore.Components 4.14.0
- **New tests added by this story:** 14 (4 NavMenu + 7 StatCard + 3 EmptyState)

### File List

- `tests/Hexalith.EventStore.Admin.UI.Tests/Layout/NavMenuTests.cs` (NEW)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/StatCardTests.cs` (NEW)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/Shared/EmptyStateTests.cs` (NEW)

## Change Log

- 2026-04-13: Created baseline bUnit render tests for NavMenu, StatCard, and EmptyState components. 14 new tests, all 592 suite tests pass on Fluent UI v4.14.0.
