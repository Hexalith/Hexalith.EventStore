# Sprint Change Proposal — Type Catalog FluentTabs Overflow Bug

**Date:** 2026-04-08
**Triggered by:** UI bug observed on `/types` page
**Scope:** Minor
**Status:** Approved

## Section 1: Issue Summary

On the Admin UI Type Catalog page (`/types`), the FluentTabs component renders only "Events(0)" and "Commands(0)" as visible tabs, collapsing "Aggregates(0)" behind a "+1" overflow button. When the "+1" button is clicked, the Aggregates tab is added to the tab bar but renders without its label text — only the selection highlight is visible.

**Root cause:** The parent `<div>` at line 77 of `TypeCatalog.razor` has no style when no detail panel is open, causing the flex item to not fill available space. The `FluentTabs` component's built-in overflow mechanism triggers prematurely with only 3 short tabs. Additionally, `FluentTabs` itself lacks an explicit `width: 100%` to stretch within its container.

## Section 2: Impact Analysis

- **Epic Impact:** Epic 15 (Admin Web UI — Core Developer Experience) — no scope change, fix restores intended behavior
- **Story Impact:** No new stories required. Fix is a 2-line CSS/layout change within the existing Type Catalog page
- **Artifact Conflicts:** None. PRD and Architecture are unaffected. UX spec compliance (UX-DR34-DR40) is restored by the fix
- **Technical Impact:** Zero risk — only CSS/layout properties changed on one Blazor component

## Section 3: Recommended Approach

**Direct Adjustment** — Modify `TypeCatalog.razor` lines 77-78.

- Effort: Low (2 lines changed)
- Risk: Low (CSS-only, no logic change)
- Timeline impact: None

## Section 4: Detailed Change Proposals

### Change 1: Fix FluentTabs container width

**File:** `src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor`

**OLD (lines 77-78):**
```razor
        <div style="@(Viewport.IsWideViewport && HasSelection ? "flex: 1; min-width: 0;" : "")">
            <FluentTabs ActiveTabId="@_activeTab" ActiveTabIdChanged="@OnTabChanged">
```

**NEW:**
```razor
        <div style="flex: 1; min-width: 0;">
            <FluentTabs ActiveTabId="@_activeTab" ActiveTabIdChanged="@OnTabChanged" Style="width: 100%;">
```

**Rationale:**
1. Parent `<div>` always gets `flex: 1; min-width: 0;` so the tabs container fills available flex space regardless of detail panel state
2. `Style="width: 100%;"` on `FluentTabs` ensures the tab bar stretches to fill its container, preventing premature overflow on 3 short tabs

## Section 5: Implementation Handoff

- **Scope classification:** Minor
- **Route to:** Development team for direct implementation
- **Responsibilities:** Apply the 2-line fix, verify all 3 tabs render correctly at various viewport widths, verify detail panel still works correctly when a type is selected
- **Success criteria:** All three tabs (Events, Commands, Aggregates) are always visible without overflow; clicking any tab shows its label text correctly
