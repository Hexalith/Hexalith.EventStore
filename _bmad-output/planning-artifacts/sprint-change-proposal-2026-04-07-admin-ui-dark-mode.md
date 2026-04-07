# Sprint Change Proposal — 2026-04-07 — Admin UI Dark Mode Fix

## Section 1: Issue Summary

**Trigger:** User reported that dark mode does not work on Admin UI pages — background stays white and left-side text (sidebar navigation) is illegible when dark mode is toggled on.

**Problem Statement:** The `FluentDesignTheme` component in `MainLayout.razor` is rendered as a self-closing sibling element alongside `FluentLayout`, rather than wrapping it. Fluent UI design tokens (background colors, foreground colors, etc.) do not propagate to layout children. Additionally, the `<body>` element in `App.razor` has no background styling, so the browser default white background persists regardless of theme state.

**Discovery:** User toggled dark mode via the in-app ThemeToggle button (OS is also in dark mode). All Admin UI pages affected — sidebar nav and page content text becomes illegible against white background.

**Evidence:**
- `FluentDesignTheme` at `MainLayout.razor:15-18` is self-closing (`/>`) — not wrapping `FluentLayout`
- `App.razor:16` has bare `<body>` with no background or color styling
- All Fluent UI child components (`FluentNavMenu`, `FluentHeader`, data grids, etc.) fail to inherit dark mode tokens

---

## Section 2: Impact Analysis

### Epic Impact
- **Epic 15 (Admin Web UI — Core Developer Experience):** Story 15-1 (Blazor Shell, FluentUI Layout, Dark Mode) was marked `done` but has this rendering bug. No scope change — fix aligns with original story acceptance criteria.
- **All other epics:** No impact. All Admin UI epics (16-20) inherit layout from MainLayout and will automatically benefit from this fix.

### Story Impact
- **Story 15-1:** Implementation gap — dark mode toggle works programmatically but design tokens don't propagate to layout children.
- No other stories affected.

### Artifact Conflicts
- **PRD:** None. Dark mode is an expected feature.
- **Architecture:** None. Fix is CSS/Razor only.
- **UX Design:** Fix aligns with spec: "Respects OS prefers-color-scheme by default; explicit toggle in v2."

### Technical Impact
- **MainLayout.razor:** `FluentDesignTheme` changes from self-closing to wrapping element.
- **App.razor:** `<body>` gets theme-aware background/color styling with dark fallbacks.
- **Risk:** None — no behavioral change, no API surface change, no new dependencies.

---

## Section 3: Recommended Approach

**Selected:** Direct Adjustment

**Rationale:**
- Two-file fix, purely structural Razor/CSS changes
- No behavioral change for existing functionality
- Follows standard Fluent UI Blazor theming pattern (FluentDesignTheme wraps content)
- Body fallback colors prevent flash-of-white during initial load

**Effort:** Low
**Risk:** Low
**Timeline:** No impact on sprint timeline

---

## Section 4: Detailed Change Proposals

### Change 1: MainLayout.razor — Wrap FluentLayout inside FluentDesignTheme

**File:** `src/Hexalith.EventStore.Admin.UI/Layout/MainLayout.razor`
**Story:** 15-1

OLD:
```razor
<FluentDesignTheme Mode="@ThemeState.Mode"
                   ModeChanged="@OnThemeModeChanged"
                   StorageName="hexalith-admin-theme"
                   CustomColor="#0066CC" />

<a href="#main-content" class="skip-to-main">Skip to main content</a>

<FluentLayout>
    ...
</FluentLayout>

<CommandPalette @ref="_commandPalette" />
```

NEW:
```razor
<FluentDesignTheme Mode="@ThemeState.Mode"
                   ModeChanged="@OnThemeModeChanged"
                   StorageName="hexalith-admin-theme"
                   CustomColor="#0066CC">

    <a href="#main-content" class="skip-to-main">Skip to main content</a>

    <FluentLayout>
        ...
    </FluentLayout>

    <CommandPalette @ref="_commandPalette" />
</FluentDesignTheme>
```

**Rationale:** Wrapping all visible content inside `FluentDesignTheme` ensures Fluent UI design tokens (`--neutral-layer-1`, `--neutral-foreground-rest`, etc.) propagate via CSS custom properties to `FluentLayout`, `FluentNavMenu`, and all page content.

---

### Change 2: App.razor — Add dark-mode-aware body background

**File:** `src/Hexalith.EventStore.Admin.UI/Components/App.razor`
**Story:** 15-1

OLD:
```html
<body>
    <FluentToastProvider />
```

NEW:
```html
<body style="background-color: var(--neutral-layer-1, #1b1b1b); color: var(--neutral-foreground-rest, #e0e0e0);">
    <FluentToastProvider />
```

**Rationale:** The `<body>` is static SSR HTML outside the interactive Blazor circuit. Setting background and text color using Fluent UI CSS variables (with dark fallbacks) prevents white background bleed-through during load and in any gaps outside the FluentDesignTheme scope.

---

## Section 5: Implementation Handoff

**Change Scope: Minor** — Direct implementation by development team.

**Action items:**
1. Apply Change 1 to `MainLayout.razor`
2. Apply Change 2 to `App.razor`
3. Verify: toggle dark mode in-app — all pages should have dark background and legible text
4. Verify: light mode still works correctly
5. Build passes (0 errors, 0 warnings)

**Success Criteria:**
- Dark mode toggle produces dark backgrounds on all Admin UI pages
- Sidebar navigation text is legible in both light and dark modes
- No flash of white background on page load when OS is in dark mode
- Build passes with zero warnings
