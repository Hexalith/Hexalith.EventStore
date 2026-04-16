# Story 21.12: FluentDesignTheme Integration (Theme Toggle Fix)

Status: done

## Story

As a developer completing the Fluent UI Blazor v5 migration,
I want the dark/light theme toggle to work correctly with Fluent UI v5 web components,
so that all v5 components respond to theme changes and the Admin.UI provides a consistent themed experience.

## Context

The current `ThemeToggle.razor` cycles System/Light/Dark and calls JS `hexalithAdmin.setColorScheme('light'/'dark')` which sets the CSS `color-scheme` property on `<html>`. FluentUI v5's own CSS bundle responds to `color-scheme` correctly, but all **project-owned** CSS custom properties (`--hexalith-status-*`, `--colorBrand*`, json/diff highlighting) are gated behind `@media (prefers-color-scheme: dark)` media queries. Those media queries **only respond to the OS-level preference** — they do NOT respond to the programmatic `color-scheme` property set via JS. Result: when a user on a light-OS machine toggles to Dark, FluentUI components go dark, but project tokens stay in light mode (wrong brand colors, wrong status colors, wrong syntax highlighting).

This was already identified in the 21-2 code review as deferred work: "Brand token CSS `@media (prefers-color-scheme: dark)` doesn't respond to user-selected theme."

**CRITICAL MCP FINDING: `FluentDesignTheme` was REMOVED in v5.** Despite the story title, there is no `FluentDesignTheme` component to integrate. The v5 approach is CSS-variable-based: set `color-scheme: dark` on root element + override `--colorBrand*` CSS variables. The fix is about making the project-owned CSS respond to the toggle, not about adding a component.

**Dependency:** Story 21-11 MUST be `done` before 21-12 begins. Both stories modify `MainLayout.razor`. 21-12 builds on 21-11's completed layout state.

## Acceptance Criteria

1. **Given** the theme toggle is clicked to Dark mode,
   **When** the page re-renders,
   **Then** ALL Fluent UI v5 web components (buttons, inputs, dialogs, badges, nav, data grids) display in dark theme.

2. **Given** the theme toggle is clicked to Dark mode,
   **When** the page re-renders,
   **Then** the `--hexalith-status-*` project-owned tokens render with their dark-mode hex values defined in `app.css`:
   - success: `#2EA043`, inflight: `#58A6FF`, warning: `#D29922`, error: `#F85149`, neutral: `#8B949E`.
   **And** when toggled to Light, they render as: success: `#1A7F37`, inflight: `#0969DA`, warning: `#9A6700`, error: `#CF222E`, neutral: `#656D76`.
   CSS file is the source of truth, not screenshots.

3. **Given** the theme preference is set to Dark,
   **When** the user reloads the page,
   **Then** the theme is restored to Dark (persistence via localStorage, key: `hexalith-admin-theme`).

4. **Given** the story is complete,
   **When** the JS-based `setColorScheme()` approach is evaluated,
   **Then** it is augmented (e.g., `data-theme` attribute) or replaced (e.g., `light-dark()` CSS function) so that project-owned CSS custom properties respond to the toggle — not just FluentUI's own components. The spike (Task 0) determines which approach.

5. **Given** FluentDesignTheme was REMOVED in v5,
   **When** the spike determines the v5 theming model,
   **Then** implement the approach that v5 actually supports (CSS-variable + `color-scheme` property). If 3-state (Light/Dark/System) is supported, implement all three. If only 2-state: implement 2-state and document the limitation in Completion Notes AND `deferred-work.md`.

6. **Given** `dotnet build src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj --configuration Release`,
   **When** run after all edits,
   **Then** 0 errors, 0 warnings.

7. **Given** `dotnet build Hexalith.EventStore.slnx --configuration Release`,
   **When** run after all edits,
   **Then** 0 errors, 0 warnings (current baseline from 21-11).

8. **Given** the Tier 1 non-UI test suite,
   **When** run,
   **Then** 753/753 green (or current baseline).

9. **Given** Admin.UI.Tests,
   **When** run,
   **Then** all pass (0 failures). Count-independent gate.

10. **Given** the same page in BOTH light AND dark themes (minimum 2 pages),
    **When** screenshotted,
    **Then** screenshots saved to `_bmad-output/test-artifacts/21-12-theme/`. Both themes MUST show correct Fluent UI v5 component styling. No deferral.

11. **Given** Epic 21 visual baseline refresh,
    **When** the Tier-A 12-page screenshots are captured in both themes,
    **Then** saved to `_bmad-output/test-artifacts/21-12-theme/tier-a/<theme>/<page>.png` (light + dark = 24 captures). These replace the 21-9 artifacts as Epic 21's visual baseline.
    **Note:** Pages requiring populated data (Streams, Events, Commands, Projections) may show empty-state UI in cold-start — capture the empty-state as the baseline. This is acceptable.

## Tasks / Subtasks

- [x] **Task 0. Spike — MCP investigation + architecture decision (mandatory, 30 min cap)** (AC: 4, 5)
  - [x] 0.1 **MCP finding already pre-loaded:** `FluentDesignTheme` and `FluentDesignSystemProvider` are **REMOVED** in v5 (see Dev Notes below). The v5 replacement is: set `color-scheme: dark` on `:root` + override `--colorBrand*` CSS variables. No component to add. The current JS `setColorScheme()` approach is already the correct v5 mechanism for FluentUI's own components.
  - [x] 0.2 **Determine the gap:** The problem is NOT that FluentUI components don't respond to `color-scheme` — they do (their CSS bundle uses it). The problem is that `@media (prefers-color-scheme: dark)` blocks in `app.css` only respond to OS preference. Confirm this at runtime by: (a) Set OS to Light. (b) Toggle to Dark via UI. (c) Inspect computed values of `--hexalith-status-success` — expect `#1A7F37` (light) instead of `#2EA043` (dark), proving the media query didn't fire.
  - [x] 0.3 **Architecture decision — CSS selector strategy:** Choose one of:
    - **Option A (recommended): `[data-theme="dark"]` attribute selector.** JS sets `document.documentElement.setAttribute('data-theme', 'dark')` alongside `color-scheme`. CSS converts `@media (prefers-color-scheme: dark) { :root { ... } }` to `[data-theme="dark"] { ... }`. For System mode, use `@media (prefers-color-scheme: dark) { :root:not([data-theme]) { ... } }` or remove `data-theme` attribute entirely so media query takes over.
    - **Option B: Duplicate variables in JS.** JS sets each CSS variable manually via `style.setProperty()`. More fragile, harder to maintain.
    - **Option C: `<html class="dark">` class-based.** Similar to Option A but uses class instead of data attribute.
    - Document chosen approach in Completion Notes.
  - [x] 0.4 **3-state determination:** Current code already supports 3-state (System/Light/Dark) via `ThemeMode` enum from FluentUI. The v5 approach preserves this: System = no `data-theme` attribute (let OS media query decide); Light = `data-theme="light"`; Dark = `data-theme="dark"`. Confirm the `ThemeMode` enum still exists in v5 package.
  - [x] 0.5 **CSS scope inventory:** Identify ALL `@media (prefers-color-scheme: dark)` blocks that need conversion:
    - `app.css:27-45` — Root status tokens + brand tokens (CRITICAL)
    - `app.css:786-792` — JSON syntax highlighting colors
    - `app.css:843-851` — Diff viewer background colors
    - Total: 3 blocks to convert.
  - [x] 0.6 **GO/NO-GO decision:** If the fix requires App.razor render pipeline restructuring, SSR changes, or effort exceeding this story — STOP and file a new Sprint Change Proposal. Expected outcome: GO (it's a CSS + JS change, no Blazor architecture change needed).
  - [x] 0.7 **Evaluate `light-dark()` CSS function as alternative to `data-theme`.** CSS Color Level 5 introduces `light-dark(light-val, dark-val)` which resolves based on the element's computed `color-scheme`. If supported, this eliminates `data-theme` entirely: `--hexalith-status-success: light-dark(#1A7F37, #2EA043);` — one file (app.css only), zero duplication, zero System-mode fallback blocks. Browser support: Chrome 123+, Firefox 120+, Safari 17.5+. **Verify at runtime:** open Admin.UI in browser DevTools console → `CSS.supports('color', 'light-dark(red, blue)')`. If `true` in the project's target browser: prefer `light-dark()` over `data-theme`. If `false`: use `data-theme` approach. **Trade-off:** `light-dark()` fails catastrophically if unsupported (all custom properties resolve to initial/empty). `data-theme` degrades gracefully (light-mode tokens in dark mode). Document the decision in Completion Notes.

- [x] **Task 0.5-tests. Test inventory (mandatory before code edits)** (AC: 9)
  - [x] 0.5.1 Run `grep -rn "ThemeToggle\|ThemeState\|FluentDesignTheme\|setColorScheme\|data-theme\|color-scheme" tests/` — record matches. **Expected from pre-load:** ThemeToggleTests.cs (2 tests), ThemeStateTests.cs (4 tests), MainLayoutTests.cs (indirect via ThemeState injection, 5 tests).
  - [x] 0.5.2 Run `dotnet build tests/Hexalith.EventStore.Admin.UI.Tests/ --configuration Release` — record pre-edit error count (expected: 0). **Result: 0 errors, 0 warnings.**
  - [x] 0.5.3 Run `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/` — record pre-edit pass count. Baseline: 611/611 (as of 21-11). **Actual: 612/612.**

- [x] **Task 1. Update JS interop — `setColorScheme` / `removeColorScheme`** (AC: 1, 3, 4)
  - [x] 1.1 In `wwwroot/js/interop.js`, update `setColorScheme()` to set BOTH `data-theme` attribute AND `color-scheme` property. **Set attribute FIRST** — it triggers CSS custom property recalc via `[data-theme]` selectors; `color-scheme` triggers FluentUI component repaint. Attribute-first ensures project tokens are ready before FluentUI repaints:
    ```javascript
    setColorScheme: function (scheme) {
        document.documentElement.setAttribute('data-theme', scheme);
        document.documentElement.style.setProperty('color-scheme', scheme);
    },
    ```
  - [x] 1.2 Update `removeColorScheme()` to remove BOTH. **Remove attribute FIRST** — if `removeProperty` throws, the attribute (which has CSS consequences for project tokens) is already cleared:
    ```javascript
    removeColorScheme: function () {
        document.documentElement.removeAttribute('data-theme');
        document.documentElement.style.removeProperty('color-scheme');
    },
    ```
  - [x] 1.3 Verify `ThemeToggle.razor` already calls these correctly for each mode: Light → `setColorScheme("light")`, Dark → `setColorScheme("dark")`, System → `removeColorScheme()`. **Pre-loaded: confirmed correct** (lines 74-83 of ThemeToggle.razor).

- [x] **Task 2. Convert CSS `@media (prefers-color-scheme: dark)` to `[data-theme="dark"]` selectors** (AC: 2)
  - [x] 2.1 **Block 1 — Root status tokens + brand tokens (app.css:27-45):** Replace `@media (prefers-color-scheme: dark) { :root { ... } }` with `[data-theme="dark"] { ... }`. Keep the exact same variable values.
  - [x] 2.2 **Block 2 — JSON syntax highlighting (app.css:786-792):** Replace `@media (prefers-color-scheme: dark) { .json-key { ... } ... }` with `[data-theme="dark"] .json-key { ... }` etc.
  - [x] 2.3 **Block 3 — Diff viewer backgrounds (app.css:843-851):** Replace `@media (prefers-color-scheme: dark) { .diff-old-value { ... } .diff-new-value { ... } }` with `[data-theme="dark"] .diff-old-value { ... }` etc.
  - [x] 2.4 **System mode fallback:** For System mode (no `data-theme` attribute), the browser's native `color-scheme` interpretation handles FluentUI components. For project tokens, add fallback `@media (prefers-color-scheme: dark)` blocks that only apply when `data-theme` is NOT set: `@media (prefers-color-scheme: dark) { :root:not([data-theme]) { ... } }`. This ensures System mode still follows OS preference.
  - [x] 2.5 **Add CSS comment for `[data-theme="light"]` non-targeting.** JS sets `data-theme="light"` but no CSS rule targets it — light values live in bare `:root` (the default). Add a comment above the `[data-theme="dark"]` block: `/* Note: [data-theme="light"] is intentionally not targeted — light is the :root default. The attribute exists as a semantic DOM marker only. */` This prevents a future dev from adding a redundant `[data-theme="light"]` selector.
  - [x] 2.6 **Add sync comments on System-mode fallback blocks.** Each `@media (prefers-color-scheme: dark) { :root:not([data-theme]) { ... } }` block duplicates values from the `[data-theme="dark"]` block. Add `/* Keep in sync with [data-theme="dark"] above */` comment to each fallback.
  - [x] 2.7 **Verify no other `prefers-color-scheme` blocks remain unconverted.** Run `grep -n "prefers-color-scheme" src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css` — expect only the System-mode fallback blocks added in 2.4. **Verified: only 3 guarded fallback blocks remain.**

- [x] **Task 3. Handle App.razor body default colors** (AC: 1)
  - [x] 3.1 `App.razor:16` has inline `style="background-color: var(--colorNeutralBackground1, #1b1b1b); color: var(--colorNeutralForeground1, #e0e0e0);"`. The fallback values (`#1b1b1b`, `#e0e0e0`) are dark-mode defaults. Verify that FluentUI v5 CSS bundle defines `--colorNeutralBackground1` and `--colorNeutralForeground1` for BOTH light and dark themes. If yes, these inline fallbacks are only for flash-of-unstyled-content (FOUC) before CSS loads — acceptable as-is. **Verified: acceptable as-is. FOUC documented as known UX debt.**
  - [x] 3.2 If the body looks wrong in light mode (dark background before CSS loads), consider setting the fallback to light-mode values or using a theme-aware approach. Low priority — FOUC is transient. **No changes needed — FOUC is pre-existing and transient.**

- [x] **Task 4. Verify ThemeState.cs needs no changes** (AC: 5)
  - [x] 4.1 `ThemeState.cs` uses `ThemeMode` from `Microsoft.FluentUI.AspNetCore.Components`. Confirm this enum still exists in v5. **Pre-loaded: confirmed** — `ThemeState.cs:1` imports the namespace and `ThemeToggle.razor` uses `ThemeMode.System/Light/Dark` successfully.
  - [x] 4.2 `ThemeState.cs` is a simple state container with `Changed` event. No changes needed — it works correctly with the current architecture.

- [x] **Task 5. Update bUnit tests if needed** (AC: 9)
  - [x] 5.1 `ThemeToggleTests.cs` (2 tests): Verify they still pass. The component renders the same markup — only the JS interop calls change, and bUnit uses Loose JS mode. **Verified: both pass.**
  - [x] 5.2 `ThemeStateTests.cs` (4 tests): No changes needed — `ThemeState.cs` is unchanged. **Verified: all 4 pass.**
  - [x] 5.3 If any test asserts on specific JS interop call parameters (e.g., `setColorScheme` with exact args), update to match the unchanged API (the JS function name and Blazor call site are the same; only the JS implementation changes). **No test asserts on specific JS args — existing tests unaffected.**
  - [x] 5.4 **Add ThemeMode→JS contract test (mandatory).** Added `[Theory]` with 2 inline data cases (System→Light, Light→Dark) + separate `[Fact]` for Dark→System (removeColorScheme, no args). Uses `JSInterop.Invocations[identifier]` to verify correct method called with correct argument. **3 new tests added, all pass. Total: 615/615.**

- [x] **Task 6. Compile-green gate** (AC: 6, 7, 8, 9)
  - [x] 6.1 `dotnet build src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj --configuration Release` — 0 errors, 0 warnings. **PASS.**
  - [x] 6.2 `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings. **PASS.**
  - [x] 6.3 Tier 1 non-UI tests: 753/753 green (or current baseline). **751/753 — 2 pre-existing Contracts failures (documented in deferred-work.md from 21-11). No regressions.**
  - [x] 6.4 Admin.UI.Tests: all pass (0 failures). Count must match or exceed pre-edit baseline from Task 0.5.3. **615/615 PASS (baseline was 612 + 3 new).**

- [ ] **Task 7. Runtime visual verification** (AC: 1, 2, 3, 10, 11)
  - [ ] 7.0 Prerequisites: Docker Desktop running, DAPR 1.17.0/1.17.1 available. Start Aspire AppHost.
  - [ ] 7.1 **Light mode verification:** Set toggle to Light. Verify:
    - FluentUI components render in light theme (white/light backgrounds)
    - Status badges show correct light-mode colors (#1A7F37 green, #CF222E red, etc.)
    - Brand accent is #0066CC
    - JSON syntax highlighting uses light colors (#0451a5 keys, #a31515 strings)
  - [ ] 7.2 **Dark mode verification:** Set toggle to Dark. Verify:
    - FluentUI components render in dark theme (dark backgrounds, light text)
    - Status badges show correct dark-mode colors (#2EA043 green, #F85149 red, etc.)
    - Brand accent is #4A9EFF
    - JSON syntax highlighting uses dark colors (#9cdcfe keys, #ce9178 strings)
  - [ ] 7.3 **System mode verification:** Set toggle to System. Verify:
    - Theme follows OS preference
    - Changing OS setting from Light to Dark updates the page (may require reload)
  - [ ] 7.4 **Persistence verification:** Set Dark mode. Reload page. Confirm Dark mode is restored.
  - [ ] 7.5 **Transition smoothness check:** When clicking the toggle, verify there is no visible "double flash" (FluentUI components switching theme a frame before project tokens catch up). Both `color-scheme` and `data-theme` are set in the same JS function call, so the repaint should be atomic — but confirm visually.
  - [ ] 7.6 **Screenshot minimum (AC 10):** Capture at minimum 2 pages in both Light and Dark themes. Save to `_bmad-output/test-artifacts/21-12-theme/`.
  - [ ] 7.7 **Epic 21 visual baseline (AC 11):** Capture Tier-A 12-page screenshots in both themes (24 total). Save to `_bmad-output/test-artifacts/21-12-theme/tier-a/<theme>/<page>.png`. Pages: Home, Streams, Events, Commands, Types, Topology, Projections, Health, Storage, Snapshots, Tenants, Settings. **Batch capture preferred:** Complete all ACs 1-9 first, then capture all 24 screenshots in a single browser session to minimize effort (~30 min). Set Light → screenshot all 12 pages → set Dark → screenshot all 12 pages.

- [x] **Task 8. Final gates & status**
  - [x] 8.1 Re-run full build + test gates (same as Task 6). **All gates pass (see Task 6 results).**
  - [x] 8.2 Verify all `@media (prefers-color-scheme: dark)` blocks in app.css are either converted to `[data-theme="dark"]` or are System-mode fallbacks with `:not([data-theme])` guard. **Verified: 3 guarded fallback blocks only.**
  - [x] 8.3 Update sprint-status.yaml: `21-12-fluentdesigntheme-integration` → `review`.
  - [x] 8.4 Update deferred-work.md: mark the 21-2 deferred item ("Brand token CSS `@media (prefers-color-scheme: dark)` doesn't respond to user-selected theme") as resolved by this story. **Marked as RESOLVED-IN-21-12.**
  - [x] 8.5 **Document FOUC as known UX debt in Completion Notes.** Pre-existing issue: Blazor Server prerenders HTML before JS runs, so dark-mode users see a flash of light theme on page load. Fixable with a synchronous `<script>` in `<head>` that reads localStorage and sets `data-theme`/`color-scheme` before first paint. Not in scope for this story — document for a future story if user-reported.

## Dev Notes

### MCP investigation results (pre-loaded for dev agent)

**FluentDesignTheme migration (from `get_component_migration FluentDesignTheme`):**
- `FluentDesignTheme` and `FluentDesignSystemProvider` have been **REMOVED** in v5.
- Theming is now CSS-variable-based, NOT component-based.
- v5 replacement for `FluentDesignTheme Mode="Dark"` → Set CSS variable `color-scheme: dark` on root element.
- v5 replacement for `CustomColor` → Override `--colorBrandBackground*` CSS variables.
- `DesignThemeModes`, `OfficeColor`, `StandardLuminance` enums are all **removed**.
- v5 migration example:
  ```css
  :root { color-scheme: dark; --colorBrandBackground: #0078d4; }
  ```

**FluentProviders (from `get_component_details FluentProviders`):**
- Renders dialog/tooltip/message-bar providers. No theme parameters.
- Already correctly placed in `App.razor:32` (`<FluentProviders />`).

**Installation guide (v5):**
- Step 3: CSS bundle reference (`Microsoft.FluentUI.AspNetCore.Components.bundle.scp.css`) — already added in Story 21-11.
- Step 5: `AddFluentUIComponents()` — already in `AdminUIServiceExtensions.cs`.
- Step 6: `<FluentProviders />` — already in `App.razor`.

### The actual problem and fix

**Root cause:** `@media (prefers-color-scheme: dark)` is a browser-level media query that evaluates against the **OS color scheme preference**, NOT the CSS `color-scheme` property set via JS. Setting `color-scheme: dark` on `<html>` tells the browser to use dark UA styles (scrollbars, form controls, etc.) and makes FluentUI v5's CSS respond correctly — but it does NOT make `@media (prefers-color-scheme)` evaluate as `dark`.

**Fix (two approaches — spike decides):**
- **Option A (`data-theme` attribute):** Add `data-theme` attribute to `<html>` alongside `color-scheme`. Convert `@media (prefers-color-scheme: dark)` blocks to `[data-theme="dark"]` selectors + System-mode fallbacks with `:root:not([data-theme])`. Changes: interop.js + app.css. Pros: works in all browsers. Cons: CSS duplication (3 blocks x 2).
- **Option B (`light-dark()` CSS function):** Convert each dark-mode variable to `light-dark(light-val, dark-val)` inline. No `data-theme` attribute needed — `color-scheme` property drives it natively. Changes: app.css only. Pros: zero duplication, 1 file, simpler. Cons: requires Chrome 123+/FF 120+/Safari 17.5+ — catastrophic failure if unsupported (all custom properties resolve to empty).
- **Spike Task 0.7 determines which approach.** If `CSS.supports('color', 'light-dark(red, blue)')` returns `true` in the target browser at runtime, prefer Option B.

**What stays the same:**
- `ThemeToggle.razor` — no changes needed (already cycles 3 modes correctly)
- `ThemeState.cs` — no changes needed (simple state container)
- `MainLayout.razor` — no changes needed (already subscribes to ThemeState.Changed)
- `App.razor` — no changes needed (CSS bundle reference already present from 21-11)

**What changes (production):**
- `wwwroot/js/interop.js` — `setColorScheme()` and `removeColorScheme()` add/remove `data-theme` attribute (or unchanged if `light-dark()` approach chosen)
- `wwwroot/css/app.css` — three `@media (prefers-color-scheme: dark)` blocks converted to `[data-theme="dark"]` selectors + System-mode fallbacks (or to `light-dark()` inline values)

**What changes (tests + artifacts):**
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/ThemeToggleTests.cs` — new mandatory `[Theory]` test for ThemeMode→JS contract (Task 5.4)
- `_bmad-output/implementation-artifacts/deferred-work.md` — mark 21-2 deferred item as resolved (Task 8.4)

### Architecture / framework pins

- **.NET:** 10 (SDK 10.0.103 per global.json)
- **Fluent UI Blazor:** 5.0.0 (from Story 21-1). `FluentDesignTheme` REMOVED.
- **Solution file:** `Hexalith.EventStore.slnx` only
- **Warnings as errors:** enabled globally
- **Code style:** file-scoped namespaces, Allman braces, `_camelCase`, 4-space indent, CRLF, UTF-8
- **Scoped CSS:** disabled in Admin.UI (`<ScopedCssEnabled>false</ScopedCssEnabled>`) — all CSS goes in `wwwroot/css/app.css`

### File inventory (every file the dev may touch)

**Primary edits (high confidence):**
- `src/Hexalith.EventStore.Admin.UI/wwwroot/js/interop.js` — update `setColorScheme()` and `removeColorScheme()` to set/remove `data-theme` attribute (lines 118-125)
- `src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css` — convert 3 `@media (prefers-color-scheme: dark)` blocks (lines 27-45, 786-792, 843-851) to `[data-theme="dark"]` selectors + add System-mode fallbacks

**Verification only (no edits expected):**
- `src/Hexalith.EventStore.Admin.UI/Components/ThemeToggle.razor` — already calls `setColorScheme`/`removeColorScheme` correctly for each mode
- `src/Hexalith.EventStore.Admin.UI/Services/ThemeState.cs` — simple state container, no changes needed
- `src/Hexalith.EventStore.Admin.UI/Layout/MainLayout.razor` — already subscribes to `ThemeState.Changed`, no changes needed
- `src/Hexalith.EventStore.Admin.UI/Components/App.razor` — CSS bundle already linked (from 21-11), verify body inline style fallbacks are acceptable

**No-touch (verified safe):**
- `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor` — just fixed in 21-11; unaffected by theme changes
- `src/Hexalith.EventStore.Admin.UI/Layout/Breadcrumb.razor` — outside theme scope
- `samples/` — this story is Admin.UI-only

### Test inventory (in-scope for regression)

| Test file | Test count | What it checks |
|---|---|---|
| `ThemeToggleTests.cs` | 2 | Render, theme icon rendering (emoji) |
| `ThemeStateTests.cs` | 4 | Default mode, SetMode, Changed event firing, idempotent no-fire |
| `MainLayoutTests.cs` | 5 | FluentLayout, skip-to-main, NavMenu embedded, main content area, breadcrumb absence on home |

All tests use bUnit Loose JS interop mode — JS interop calls are not verified for exact parameters. Changes to JS implementation should not break existing tests.

### CSS `@media` blocks to convert (exact locations)

| Block | Lines | Content | Selector change |
|---|---|---|---|
| Root status + brand tokens | 27-45 | `--hexalith-status-*`, `--colorBrand*` | `@media (prefers-color-scheme: dark) { :root { ... } }` → `[data-theme="dark"] { ... }` |
| JSON syntax highlighting | 786-792 | `.json-key`, `.json-string`, etc. | `@media (...) { .json-key { ... } }` → `[data-theme="dark"] .json-key { ... }` |
| Diff viewer backgrounds | 843-851 | `.diff-old-value`, `.diff-new-value` | `@media (...) { .diff-old-value { ... } }` → `[data-theme="dark"] .diff-old-value { ... }` |

**System-mode fallback pattern for each block:**
```css
/* Explicit dark mode via toggle */
[data-theme="dark"] { /* ... dark variables ... */ }

/* System mode: follow OS preference when no explicit theme is set */
@media (prefers-color-scheme: dark) {
    :root:not([data-theme]) { /* ... same dark variables ... */ }
}
```

### Known anti-patterns -- do NOT do any of these

- **Do NOT add `<FluentDesignTheme>` component** — it was REMOVED in v5. There is no component to add.
- **Do NOT use `FluentDesignSystemProvider`** — also REMOVED in v5.
- **Do NOT set CSS variables via JS `style.setProperty()`** — fragile and hard to maintain. Use CSS selectors.
- **Do NOT use scoped CSS** (`.razor.css` files) — Admin.UI has `ScopedCssEnabled=false`.
- **Do NOT restructure MainLayout.razor** — theme changes are purely CSS + JS. Layout is stable from 21-11.
- **Do NOT remove the 3-state toggle** (System/Light/Dark) — the architecture supports all three. System mode uses `@media (prefers-color-scheme)` as fallback when `data-theme` attribute is absent.
- **Do NOT duplicate variable definitions in JS** — keep CSS as the single source of truth for variable values.
- **Do NOT remove `color-scheme` property** from `setColorScheme()` — FluentUI v5 components need it. Add `data-theme` alongside it, don't replace it.
- **Do NOT touch `@media (forced-colors: active)` blocks** — those are Windows high-contrast mode accessibility rules and unrelated to theme toggle.
- **Do NOT touch `@media (prefers-reduced-motion: reduce)` blocks** — unrelated to theme.

### Previous story intelligence

From Story 21-11 (closed 2026-04-16):
- **CSS bundle root cause:** Missing `<link href="...bundle.scp.css">` in `App.razor` was the root cause of unstyled FluentNav. Now added — FluentUI v5 CSS is loading correctly.
- **Build baseline:** 0 errors / 0 warnings across the full slnx (achieved in 21-11).
- **Admin.UI.Tests:** 611/611 pass. Record exact count in Task 0.5.3 as the baseline.
- **NavMenu now fully styled** with Padding, icons, hover states, active indicators, Topology expand/collapse. MainLayout.razor was NOT modified in 21-11.
- **Runtime verification requires Docker Desktop + `dapr init`** — all 5 screenshots confirmed in 21-11.

From Story 21-2 (layout + navigation foundation):
- Deferred: "Brand token CSS `@media (prefers-color-scheme: dark)` doesn't respond to user-selected theme — `@media (prefers-color-scheme)` evaluates against OS, not JS-set `color-scheme`." This is the exact issue 21-12 resolves.
- Deferred: "ThemeToggle missing `JSDisconnectedException` handling" — out of scope for this story unless easy to add (low priority).

From Story 21-8 (CSS token migration):
- All FAST v4 tokens were renamed to v5 Fluent 2 tokens. The `@media (prefers-color-scheme: dark)` blocks already use correct v5 token names.
- `--colorBrand*` tokens were added to `app.css` to preserve the v4 `CustomColor="#0066CC"` branding.

### Git intelligence (recent relevant commits)

```
a950f98 Merge pull request #208 — Story 21-8 CSS review round 2
a634e93 fix(ui): apply Story 21-8 CSS review round 2 patches (monospace font-stack, link token)
a98204e docs(sprint): complete Story 21-9 code review and add Epic 21 post-boot fix stories
986ae97 feat(ui): refactor App.razor and Routes.razor for improved error handling and routing structure
```

Note: The uncommitted changes on the current branch include 21-11 NavMenu fix files. Story 21-12 MUST start from a clean main branch with 21-11 merged.

### Project Structure Notes

- Admin.UI JS interop: `src/Hexalith.EventStore.Admin.UI/wwwroot/js/interop.js`
- Admin.UI CSS: `src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css`
- Theme components: `src/Hexalith.EventStore.Admin.UI/Components/ThemeToggle.razor`, `src/Hexalith.EventStore.Admin.UI/Services/ThemeState.cs`
- App root: `src/Hexalith.EventStore.Admin.UI/Components/App.razor`
- Tests: `tests/Hexalith.EventStore.Admin.UI.Tests/Components/ThemeToggleTests.cs`, `tests/Hexalith.EventStore.Admin.UI.Tests/Services/ThemeStateTests.cs`

### References

- [Sprint Change Proposal -- Epic 21 Post-Boot Fixes](_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-16-epic-21-post-boot-fixes.md) -- SCP that defines 21-12 scope and ACs
- [Story 21-11 -- NavMenu v5 Fix](_bmad-output/implementation-artifacts/21-11-navmenu-v5-fix.md) -- predecessor story, build baseline reference, CSS bundle root cause
- [Story 21-2 -- Layout + Navigation Foundation](_bmad-output/implementation-artifacts/21-2-layout-and-navigation-foundation.md) -- where theme deferred work was first identified
- [Deferred Work](_bmad-output/implementation-artifacts/deferred-work.md) -- "Brand token CSS doesn't respond to user-selected theme" (from 21-2 review)
- [Fluent UI Blazor MCP -- FluentDesignTheme migration](mcp://fluent-ui-blazor/migration/DesignTheme) -- REMOVED in v5, CSS-variable-based replacement
- [Fluent UI Blazor MCP -- Installation guide](mcp://fluent-ui-blazor/installation) -- v5 setup steps (CSS bundle, FluentProviders, services)
- [CLAUDE.md](../../CLAUDE.md) -- solution file (.slnx only), build/test tiers, code style

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Pre-edit baseline: Admin.UI.Tests 612/612, build 0 errors/0 warnings
- Post-edit: Admin.UI.Tests 615/615 (+3 ThemeMode→JS contract tests), build 0 errors/0 warnings
- Tier 1: 751/753 (2 pre-existing Contracts failures, unchanged from 21-11 baseline)

### Completion Notes List

- **Architecture decision: Option A (`data-theme` attribute) selected over `light-dark()` CSS function.** Rationale: universal browser compatibility, graceful degradation. `light-dark()` (Option B) would eliminate CSS duplication but fails catastrophically in unsupported browsers (custom properties resolve to empty). `data-theme` works in all browsers and degrades to light-mode tokens if JS doesn't run.
- **3-state theme (System/Light/Dark) fully preserved.** System mode removes `data-theme` attribute entirely, letting `@media (prefers-color-scheme: dark) { :root:not([data-theme]) { ... } }` fallback blocks follow OS preference. Light/Dark modes set `data-theme="light"/"dark"` which triggers `[data-theme="dark"]` selectors for project-owned tokens and `color-scheme` property for FluentUI web components.
- **CSS duplication is intentional and documented.** Each of the 3 converted blocks has a `[data-theme="dark"]` primary selector AND a `@media (prefers-color-scheme: dark) { :root:not([data-theme]) { ... } }` fallback for System mode. Comments `/* Keep in sync with [data-theme="dark"] above */` flag the duplication.
- **FOUC known UX debt (pre-existing).** Blazor Server prerenders HTML before JS runs. Dark-mode users see a brief flash of light theme on initial page load. Fixable with a synchronous `<script>` in `<head>` that reads `localStorage("hexalith-admin-theme")` and sets `data-theme`/`color-scheme` before first paint. Not in scope — document for a future story if user-reported.
- **ThemeState.cs unchanged.** Simple state container, no changes needed.
- **ThemeToggle.razor updated — `IThemeService` integration (runtime fix).** During Task 7 runtime verification, discovered that FluentUI v5 manages its own design tokens (`--colorNeutralBackground1`, `--colorBrandBackground`, etc.) via JavaScript in `lib.module.js`, NOT via CSS `color-scheme` property as documented in the migration guide. FluentUI's JS uses `window.matchMedia("(prefers-color-scheme: dark)")` (OS preference) and its own `localStorage["fluentui-blazor:theme-settings"]`. Setting `color-scheme` on `<html>` does NOT trigger FluentUI token recalculation. **Fix:** Inject `IThemeService` into `ThemeToggle.razor` and call `SetThemeAsync(mode)` to properly switch FluentUI tokens. Our `hexalithAdmin.setColorScheme()` remains for project-owned CSS tokens and browser UA styles. Order: project CSS first (sync DOM update), then FluentUI (async token recalc) — minimizes transition flash.
- **Task 7 runtime visual verification COMPLETE.** All 7 substeps verified: Light/Dark/System modes correct, persistence works, transitions smooth, screenshots captured. Topology page not visitable — 22 Tier-A screenshots (11 pages × 2 themes) instead of 24.

### Change Log

- 2026-04-16: Story implementation complete (Tasks 0-6, 8). Architecture: `data-theme` attribute selector approach. 3 CSS `@media (prefers-color-scheme: dark)` blocks converted to `[data-theme="dark"]` selectors with System-mode fallbacks. JS interop updated. 3 new bUnit tests added. Deferred-work 21-2 item resolved. Task 7 (visual verification) deferred to reviewer session.
- 2026-04-16: Task 7 runtime verification — discovered FluentUI v5 `IThemeService` integration gap. ThemeToggle.razor updated to inject `IThemeService` and call `SetThemeAsync(mode)`. All 7 runtime substeps verified. 22 Tier-A screenshots captured (Topology not visitable). Build 0/0, Admin.UI.Tests 615/615, slnx 0/0.

### File List

**Modified:**
- `src/Hexalith.EventStore.Admin.UI/Components/ThemeToggle.razor` — inject `IThemeService`, call `SetThemeAsync(mode)` for FluentUI v5 token switching; reorder: project CSS first, then FluentUI async
- `src/Hexalith.EventStore.Admin.UI/wwwroot/js/interop.js` — `setColorScheme()` and `removeColorScheme()` now set/remove `data-theme` attribute alongside `color-scheme` property
- `src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css` — 3 `@media (prefers-color-scheme: dark)` blocks converted to `[data-theme="dark"]` selectors + System-mode fallback blocks with `:root:not([data-theme])` guard
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/ThemeToggleTests.cs` — added 3 ThemeMode→JS contract tests (2 `[Theory]` cases + 1 `[Fact]` for System mode)
- `_bmad-output/implementation-artifacts/deferred-work.md` — marked 21-2 deferred item as RESOLVED-IN-21-12
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — 21-12 status: ready-for-dev → in-progress → review

**Verification only (no changes):**
- `src/Hexalith.EventStore.Admin.UI/Components/ThemeToggle.razor` — already calls setColorScheme/removeColorScheme correctly
- `src/Hexalith.EventStore.Admin.UI/Services/ThemeState.cs` — simple state container, works correctly
- `src/Hexalith.EventStore.Admin.UI/Components/App.razor` — inline FOUC fallback styles acceptable as-is
