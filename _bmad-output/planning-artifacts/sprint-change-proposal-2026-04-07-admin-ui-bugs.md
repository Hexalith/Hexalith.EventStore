# Sprint Change Proposal — 2026-04-07 — Admin UI Status & Theme Bugs

## Section 1: Issue Summary

**Trigger:** During general Admin UI usage, two pre-existing bugs were identified in the shared layout/shell components (Epic 15, Story 15-1).

**Problem Statement:**
1. **Status indicator stuck on "Unknown":** The `HeaderStatusIndicator` in the top-right corner of all admin pages displays "Unknown" permanently on any page other than the dashboard (`/`). This is because `DashboardRefreshService.Start()` is only called in `Index.razor`, not globally. Without active polling, the status never transitions from its initial "Unknown" state when SignalR is not connected.
2. **Theme toggle button unresponsive:** The `ThemeToggle.razor` component does not subscribe to `ThemeState.Changed`. When clicked, the underlying Fluent UI theme changes (via `App.razor` which does subscribe), but the toggle button icon/label never re-renders, appearing frozen to the user.

**Discovery:** Manual observation during Admin UI navigation across multiple pages.

**Evidence:**
- Status badge shows "Unknown" (gray/neutral) on Health, Streams, Projections, and all non-dashboard pages
- Theme toggle button click produces no visible feedback (icon does not change), though the actual theme may update behind the scenes

**UX Spec Violations:**
- Line 631: *"Connection status, registered services count, and system health are always visible in the header bar regardless of content area state"*
- Lines 541-545: Theme strategy requires explicit toggle override with visual feedback

---

## Section 2: Impact Analysis

### Epic Impact
- **Epic 15 (Admin Web UI — Core Developer Experience):** Story 15-1 (Blazor Shell, FluentUI Layout, Command Palette, Dark Mode) — two latent bugs in completed work. Epic status unchanged (in-progress for other stories).
- **Epics 16, 19, 20** (depend on Epic 15 shell): No structural impact, but all admin pages benefit from the status fix.
- **All other epics:** No impact.

### Story Impact
- **Story 15-1 (Blazor Shell, FluentUI Layout, Command Palette, Dark Mode):** Implementation gap — two bugs need patching. Status: remains "done" (bug fixes, not scope change).
- No current or future stories require modification.

### Artifact Conflicts
- **PRD:** None. FR68 (status indicators) and FR75 (health dashboard) are correctly specified; fixes align implementation with spec.
- **Architecture:** None. No architecture-level changes needed.
- **UX Design:** None. UX spec correctly defines expected behavior; implementation was incomplete.

### Technical Impact
- **Hexalith.EventStore.Admin.UI** project only:
  - `Layout/MainLayout.razor` — add `RefreshService.Start()` call
  - `Components/ThemeToggle.razor` — add `ThemeState.Changed` subscription + `IDisposable`
- No API surface changes, no NuGet package changes, no breaking changes.
- No existing test coverage for these UI behaviors.

---

## Section 3: Recommended Approach

**Selected:** Direct Adjustment

**Rationale:**
- Both bugs have clear, isolated root causes with straightforward fixes
- Correct implementation patterns already exist in the codebase (`App.razor` for event subscription, `Index.razor` for polling start)
- No dependencies on external systems or other components
- Fixes are purely additive — no behavioral change for existing functionality

**Effort:** Low (two targeted component changes)
**Risk:** Low (patterns already proven in adjacent code)
**Timeline:** No impact on sprint timeline

---

## Section 4: Detailed Change Proposals

### Change 1: Fix Status Indicator on Non-Dashboard Pages

**File:** `src/Hexalith.EventStore.Admin.UI/Layout/MainLayout.razor`
**Story:** 15-1
**Section:** `OnAfterRenderAsync` initialization

OLD:
```csharp
// MainLayout subscribes to RefreshService events but does NOT call Start()
// RefreshService.Start() is only called in Index.razor (dashboard page)
```

NEW:
```csharp
// MainLayout calls RefreshService.Start() in OnAfterRenderAsync
// This ensures polling is active on all admin pages, not just the dashboard
```

**Rationale:** `DashboardRefreshService.Start()` must be called globally (in the layout) so that polling health data occurs on every admin page. Without it, the status indicator has no data source when SignalR is unavailable, and remains stuck on "Unknown". This directly violates UX spec line 631.

---

### Change 2: Fix Theme Toggle Button Re-rendering

**File:** `src/Hexalith.EventStore.Admin.UI/Components/ThemeToggle.razor`
**Story:** 15-1
**Section:** Component lifecycle

OLD:
```csharp
// ThemeToggle calls ThemeState.SetMode() on click
// but does NOT subscribe to ThemeState.Changed
// Result: button icon never updates after click
```

NEW:
```csharp
// ThemeToggle subscribes to ThemeState.Changed in OnInitialized
// Handler calls InvokeAsync(StateHasChanged) to re-render
// Implements IDisposable to unsubscribe on dispose
// (Pattern copied from App.razor lines 50-93)
```

**Rationale:** Without subscribing to `ThemeState.Changed`, the Blazor rendering pipeline has no reason to re-evaluate the component after the theme state changes. The subscription pattern is already proven in `App.razor`.

---

## Section 5: Implementation Handoff

**Change Scope: Minor** — Direct implementation by development team.

**Handoff:** Development team (Amelia / dev agent)

**Implementation tasks:**
1. In `MainLayout.razor` `OnAfterRenderAsync`: call `RefreshService.Start()` so polling runs globally
2. In `ThemeToggle.razor`: add `ThemeState.Changed` event subscription, `StateHasChanged()` handler, and `IDisposable` cleanup
3. Verify: status badge transitions from "Unknown" to "Connected" or "Disconnected" on non-dashboard pages
4. Verify: theme toggle button icon updates immediately on click across all pages
5. Build passes (0 errors, 0 warnings)

**Success Criteria:**
- Status indicator displays correct connection state on all admin pages (not just dashboard)
- Theme toggle button visually responds to clicks with correct icon/label update
- Both light and dark themes apply correctly when toggled
- No regressions in existing Tier 1 tests
