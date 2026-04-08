# Sprint Change Proposal — Breadcrumb Home Page Consistency

**Date:** 2026-04-08
**Triggered by:** Layout inconsistency observed on Home page
**Scope:** Minor
**Status:** Approved

---

## 1. Issue Summary

The breadcrumb bar (`<Breadcrumb />`) was conditionally hidden on the Home page (`/`) via `ShouldShowBreadcrumb` in `MainLayout.razor`. All other pages displayed the breadcrumb (e.g., "Home / Commands"), creating a vertical layout shift — Home page content sat higher than every other page due to the missing breadcrumb bar height. This broke visual cohesion across the Admin UI.

**Discovered:** During page navigation review.
**Root cause:** UX spec ambiguity ("Displayed below the page header when navigating below top-level pages") was interpreted as "hide on Home," without considering the layout consequence.

---

## 2. Impact Analysis

### Epic Impact
- **Epic 15 (Admin Web UI):** No scope change. Story 15.8 (deep linking and breadcrumbs) was already complete; this is a refinement.

### Story Impact
- No new stories required. Fix is a minor correction within Story 15.8 scope.

### Artifact Conflicts
- **UX Design Specification:** Ambiguous breadcrumb visibility wording required clarification.
- **PRD / Architecture:** No conflicts.

### Technical Impact
- Single layout file change (`MainLayout.razor`): remove conditional rendering + unused property.

---

## 3. Recommended Approach

**Selected:** Direct Adjustment

- **Effort:** Low (2 edits in 1 file + 1 spec clarification)
- **Risk:** Low — the `Breadcrumb.razor` component already handles the root path correctly by showing "Home" as the sole segment
- **Timeline impact:** None

**Alternatives considered:**
- Rollback: Not applicable (nothing to revert)
- MVP Review: Not needed (scope unchanged)

---

## 4. Detailed Change Proposals

### 4.1 Code — `MainLayout.razor`

**File:** `src/Hexalith.EventStore.Admin.UI/Layout/MainLayout.razor`

**Change A — Always render breadcrumb (line 39):**

OLD:
```razor
@if (ShouldShowBreadcrumb)
{
    <Breadcrumb />
}
```

NEW:
```razor
<Breadcrumb />
```

**Change B — Remove dead property (line 69):**

OLD:
```csharp
private bool ShouldShowBreadcrumb => NavigationManager.ToBaseRelativePath(NavigationManager.Uri).Length > 0;
```

REMOVED.

**Rationale:** The breadcrumb component already renders "Home" as a standalone segment at the root path. Removing the conditional ensures consistent vertical layout across all pages.

### 4.2 UX Spec — Breadcrumb Trail section

**File:** `_bmad-output/planning-artifacts/ux-design-specification.md` (line 1461)

OLD:
```
Displayed below the page header when navigating below top-level pages:
```

NEW:
```
Displayed below the page header on every page for consistent layout. On the Home page, shows "Home" as the sole segment:
```

**Rationale:** Removes ambiguity that led to the conditional rendering decision.

---

## 5. Implementation Handoff

**Scope classification:** Minor — direct implementation, no backlog changes.

**Changes already applied:**
- [x] `MainLayout.razor` — conditional removed, property removed
- [x] `ux-design-specification.md` — breadcrumb section clarified

**Success criteria:**
- Home page displays breadcrumb bar with "Home" label
- No vertical layout shift when navigating between Home and other pages
- All existing breadcrumb functionality (truncation, copy link, deep linking) unaffected
