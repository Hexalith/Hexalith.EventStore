# Sprint Change Proposal — Admin UI Types Page Redirect-Back & Responsive Sidebar Bug Bundle

**Date:** 2026-05-06
**Triggered by:** Hands-on testing of `/types` page and observation of sidebar responsive behavior in Chrome at multiple viewport widths
**Scope Classification:** Admin UI usability fix (localized Admin UI only; no contract, server, or storage changes)
**Risk Classification:** Medium — one bug locks the user on `/types` and produces JSDisconnectedException server-side; the others are visual / state-management defects in the sidebar
**Mode:** Batch (one consolidated story; all proposals already implemented in branch `fix/admin-ui-stream-page-bug-bundle`)

---

## Section 1: Issue Summary

Three independent bugs were discovered in the Admin UI during a session of hands-on testing on `/types` and across viewport widths (~960px, ~1100px, ~1400px). They are unrelated in code but share the `Admin.UI` project, so they are bundled into a single change proposal.

### Bug A — Cannot leave `/types` page; tab oscillates on every external click

**User-visible symptom (Trace `f4ec343023d478e40b6b19ddb7c9a1dd`):**
> *"Je ne peux plus quitter la page de Type — dès que je suis dessus ça me ramène à `/types?tab=aggregates` automatiquement et l'onglet sur la page alterne entre Commands et Aggregates à chaque clic extérieur."*

**Server-side evidence:** four log entries during `/_blazor/disconnect`:

```
Microsoft.JSInterop.JSDisconnectedException: JavaScript interop calls cannot be issued at this time.
This is because the circuit has disconnected and is being disposed.
   at RemoteJSRuntime.BeginInvokeJS(...)
   at RemoteNavigationManager.PerformNavigationAsync(...)
"Navigation failed when changing the location to /types"
"Navigation failed when changing the location to /types?tab=commands"
"Unhandled exception in circuit ..."
```

**Root cause** — `TypeCatalog.razor` has an `UpdateUrl()` helper called by every nav-item click and every tab change. It calls `NavigationManager.NavigateTo("/types?...")` after computing a target URL from current state. A previous fix added an idempotency guard (skip if target == current). It did **not** guard against the user already having navigated away from `/types`.

When the user clicks a NavMenu item like `/streams`:
1. Blazor performs SPA navigation; `NavigationManager.Uri` becomes `/streams`. The `TypeCatalog` component begins its teardown.
2. **During teardown**, FluentTabs v5 re-fires `ActiveTabIdChanged` with a different `tabId`. This is a known FluentUI Blazor behavior already documented in the existing `UpdateUrl` comment: *"Some FluentTabs v5 render paths re-fire ActiveTabIdChanged after a replace-navigation, which would otherwise cycle through UpdateUrl indefinitely."*
3. `OnTabChanged("commands")` runs → updates `_activeTab` → calls `UpdateUrl(pushHistory: true)`.
4. `UpdateUrl` compares target `/types?tab=commands` to current `/streams`. Different → guard inactive → `NavigateTo("/types?tab=commands")` is called.
5. The user is yanked back to `/types`. The page re-renders. FluentTabs fires `ActiveTabIdChanged` again with another tabId. Cycle repeats; tabs alternate Commands ↔ Aggregates on every external click.
6. The `JSDisconnectedException` in the trace appears whenever the navigation roundtrip happens to outrun circuit teardown.

**Why this is severe.** It is impossible to leave the `/types` page through any sidebar nav link. Only a hard URL change in the address bar works. The page produces unhandled circuit exceptions that pollute the server logs.

### Bug B — Sidebar fights between C# state and CSS media queries

**User-visible symptom (screenshot at ~960px viewport):**
> *"La sidebar est complètement coupée — je vois 'Ho', 'Co', 'Ev'… (les 2 premiers caractères de chaque label), pas d'icônes, et quand j'agrandis la fenêtre la sidebar reste comme ça au lieu de se remettre à son état d'origine."*

**Root cause** — the sidebar had **two independent control mechanisms** in conflict:

| Mechanism | Source | Effect |
|-----------|--------|--------|
| C# state (`_sidebarCollapsed` in `MainLayout.razor:29-30`) | localStorage + Ctrl+B | Sets inline `Width="48px"` or `"220px"` + class `.collapsed` |
| CSS media query (`app.css:267-271`) | viewport width | Forces `.admin-sidebar { width: 48px; min-width: 48px }` at `<1280px` |

Outcomes by configuration:

- **Wide + expanded** (`_sidebarCollapsed=false`, viewport ≥1280): C# 220px wins → correct.
- **Wide + collapsed** (`_sidebarCollapsed=true`, viewport ≥1280): C# 48px → correct (manual Ctrl+B).
- **Narrow + collapsed** (viewport <1280, `_sidebarCollapsed=true`): both agree at 48px → correct.
- **Narrow + expanded** (viewport <1280, `_sidebarCollapsed=false`, e.g. user previously did Ctrl+B at narrow to expand): C# tries 220px, CSS forces 48px. Container is 48px; FluentNav content rendered at 220px is clipped. **Result: "Ho Co Ev" cramped strip.**

Additionally, `MainLayout` reads the saved sidebar state **once** in `OnAfterRenderAsync(firstRender)` and never re-evaluates. There is **no** subscription to `ViewportService.OnViewportChanged` (the service exists at `Services/ViewportService.cs` and already implements a 1280px-threshold breakpoint event consumed by `Breadcrumb.razor`, `StorageTreemap.razor`, `StreamDetail.razor`, etc.). So when the user resizes the window from narrow back to wide, the sidebar remains in whatever (broken) state it had on first render.

### Bug C — Narrow viewport display: even when C# state is consistent, the result is unusable

After Bug B's mechanism conflict was removed, a residual problem remains: **FluentNav has no real icon-only collapsed mode.** Shrinking the sidebar to 48px does not switch FluentNavItems to icon-only. They keep rendering icon + label in their default flex layout. Width 48px just clips the children. The user reported:

> *"Le texte est trop à droite donc illisible. Déplace le tout à gauche, déjà juste après le bleu qui donne la page sur laquelle nous sommes, et essaye d'afficher chaque texte au complet."*

**Required behavior** (clarified through iterative discussion):

| State | Width | Icons | Labels | Default at viewport |
|-------|-------|-------|--------|---------------------|
| **Normal** | 220px | shown | shown | ≥1280px |
| **Compact-text** *(new)* | 140px (fixed) | hidden | shown, left-aligned just after blue active indicator | <1280px |

`Ctrl+B` toggles Normal ↔ Compact-text **regardless of viewport**, persisted per-tier in localStorage.

The "Dashboard optimized for wider screens." banner moves with this design from the `<960px` threshold to the `<1280px` threshold (it now fires together with Compact-text). Phone-class viewports remain out of scope per user direction.

---

## Section 2: Impact Analysis

### Files modified

| File | Change | Bug |
|------|--------|-----|
| `src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor` | Added guard in `UpdateUrl()`: returns early if `currentUri.AbsolutePath != "/types"` | A |
| `src/Hexalith.EventStore.Admin.UI/Layout/MainLayout.razor` | Inject `ViewportService`, subscribe to `OnViewportChanged`, refactor sidebar load into `LoadSidebarStateForCurrentTierAsync()` called on resize, unsubscribe in `DisposeAsync`, change collapsed-state width from 48px → 140px (Compact-text mode) | B + C |
| `src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css` | `.admin-sidebar.collapsed` width 48→140; new rule hides `fluent-nav-item [slot="start"]` (icons) inside `.collapsed`; banner threshold moved from `@media (max-width: 959px)` to `@media (max-width: 1279px)`; removed the conflicting `width: 48px` from the same media query | B + C |
| `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TypeCatalogPageTests.cs` | New test `TypeCatalogPage_UpdateUrl_DoesNotNavigate_WhenUserHasLeftTypesPage` — simulates the FluentTabs teardown re-fire and asserts no `LocationChanged` is raised | A |

### Tests

- **Tier 1 (bUnit) — `TypeCatalogPageTests`**: 22/22 pass (16 pre-existing + 1 new redirect-back regression + 5 others). New test covers the exact scenario in the trace.
- **Tier 1 — `MainLayoutTests`**: not added in this session. The viewport-reactive sidebar logic is harder to test in bUnit because it depends on JS interop (`hexalithAdmin.getViewportWidth`, `setLocalStorage`) and the FluentLayout structure. Validated manually in browser. *Follow-up item: add bUnit tests once a JS-interop double exists for the sidebar storage helpers.*
- **Tier 2 / Tier 3**: untouched. No backend or DAPR changes.
- **Build**: full `dotnet build Hexalith.EventStore.slnx --configuration Debug` returns 0 warning, 0 error.

### Artifact / contract impact

- **PRD / Epics / Architecture / UX docs**: none affected. This is bug remediation in already-shipped components, not feature scope. No contract changes (`Hexalith.EventStore.Contracts.Admin` and `Hexalith.EventStore.Contracts` untouched).
- **NuGet packages**: none changed. Admin.UI is not a published NuGet package.
- **Container images**: `registry.hexalith.com/eventstore-admin-ui` will need a new build at the next release tag, picking up these patches automatically through the existing `Directory.Build.targets` container support.

### Browser / runtime impact

- **localStorage backward compatibility**: the boolean key `hexalith-sidebar-collapsed-{tier}` is reused with a semantic shift. Pre-fix `true` meant *"icon-only collapsed at 48px"*; post-fix `true` means *"Compact-text at 140px"*. Existing user preferences mostly translate sensibly (a user who collapsed at narrow will now see Compact-text at narrow), but some legacy `false` values for narrow tiers cause the new default-to-compact NOT to apply on first load.
  - **Mitigation**: documented browser-side reset in the test plan (`localStorage.removeItem` for keys prefixed `hexalith-sidebar-`). No automated migration shipped — the cost is one F12 paste once per browser per tester.
- **Banner threshold change**: previously appeared at `<960px`, now appears at `<1280px`. Operators on 1366×768 laptops (common) will see the banner where they did not before. This is intentional and aligned with the Compact-text default.

### Operator workflow impact

| Workflow | Before | After |
|----------|--------|-------|
| Open `/types`, click NavMenu item | Forced back to `/types`, tab oscillates | Navigates as expected |
| Open `/types?tab=aggregates` directly | Same redirect-loop | Loads correctly |
| Resize from wide to narrow | Sidebar shows clipped "Ho Co Ev…" | Sidebar smoothly transitions to Compact-text 140px |
| Resize from narrow back to wide | Sidebar stays in narrow state | Sidebar restores to Normal 220px |
| `Ctrl+B` on wide | Toggle expanded ↔ icons-48px | Toggle Normal ↔ Compact-text |
| `Ctrl+B` on narrow | Toggle (broken display either way) | Toggle Compact-text ↔ Normal |

---

## Section 3: Recommended Approach

**Direct Adjustment** — patch `TypeCatalog.razor`, `MainLayout.razor`, `app.css`, and add the regression test. All work was already executed and validated in this session.

**Rationale.**
- Each bug is localized and well-understood. No architectural rework needed.
- The `Bug A` fix is a 5-line guard with a deterministic regression test.
- The `Bug B + C` fix consolidates the sidebar to a single source of truth (C# state, viewport-driven) and replaces the broken icon-only collapse with a usable Compact-text mode the user explicitly designed.
- Total diff is ~60 lines across 4 files. Easily reviewable.
- The branch (`fix/admin-ui-stream-page-bug-bundle`) is already a bug-bundle branch; bundling these in is consistent with its purpose.

**Alternatives considered and rejected.**
- *Use FluentNav's built-in icon-only mode at narrow*: FluentUI Blazor v5 does not expose a clean icon-only mode for `<FluentNav>`. Custom `Width=` shrinkage was already what we had and is what produced the bug.
- *Hide the sidebar entirely on narrow + add a hamburger toggle*: bigger UX change, requires routing-aware open/close state, breaks keyboard navigation patterns the operator team is used to. Out of scope.
- *Keep separate localStorage keys per semantic version (e.g., `hexalith-sidebar-mode-v2-{tier}`)*: avoids legacy-state confusion but doubles the persistence surface and is overkill for ~5 testers.

**Effort.** Already implemented and validated. Net cost remaining: review + commit + browser AC.

**Risk.** Low.
- Bug A regression test pins the fix. The `OnViewportChanged` subscription is symmetric to existing patterns in `Breadcrumb.razor` and `StreamDetail.razor` (proven in production).
- The CSS `[slot="start"]` selector targets the FluentUI Blazor v5 light-DOM slot; if a future FluentUI version moves the icon into shadow DOM, the rule degrades gracefully (icons reappear, no functional break).
- Banner threshold change is reversible by editing one media-query selector if operators object after rollout.

---

## Section 4: Detailed Change Proposals

### Patch 1 — `TypeCatalog.razor` redirect-back guard

**File:** `src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor`
**Method:** `UpdateUrl(bool pushHistory = false)`
**Section:** Inside the navigation guard block, after the existing `Uri targetUri = ...` assignment, before the same-URL idempotency check.

```csharp
Uri currentUri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
Uri targetUri = NavigationManager.ToAbsoluteUri(url);

// Guard against teardown re-navigation: when the user navigates away from /types
// (e.g. clicks a NavMenu link), FluentTabs v5 may still fire ActiveTabIdChanged
// during the component's teardown. Without this check, OnTabChanged → UpdateUrl
// would call NavigateTo("/types?...") and yank the user back to this page,
// alternating tabs on each external NavMenu click.
if (!currentUri.AbsolutePath.Equals("/types", StringComparison.OrdinalIgnoreCase))
{
    return;
}

bool pathMatches = string.Equals(currentUri.AbsolutePath, targetUri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
bool queryMatches = string.Equals(currentUri.Query, targetUri.Query, StringComparison.Ordinal);
if (pathMatches && queryMatches)
{
    return;
}

NavigationManager.NavigateTo(url, forceLoad: false, replace: !pushHistory);
```

**Rationale.** Same intent as the pre-existing idempotency guard, broader scope: idempotency stops same-URL loops; this stops cross-page yank-back loops.

### Patch 2 — `MainLayout.razor` viewport-reactive sidebar

**File:** `src/Hexalith.EventStore.Admin.UI/Layout/MainLayout.razor`

1. **Inject ViewportService** alongside the existing services:

```diff
+ @inject ViewportService ViewportService
```

2. **Width on FluentLayoutItem and NavMenu** — change from `"48px"` to `"140px"` for the collapsed branch (was icon-only collapsed; now Compact-text):

```diff
- <FluentLayoutItem ... Width="@(_sidebarCollapsed ? "48px" : "220px")" Class=...>
-     <NavMenu Width="@(_sidebarCollapsed ? "48px" : "220px")" UserRole="_userRole" />
+ <FluentLayoutItem ... Width="@(_sidebarCollapsed ? "140px" : "220px")" Class=...>
+     <NavMenu Width="@(_sidebarCollapsed ? "140px" : "220px")" UserRole="_userRole" />
```

3. **In `OnAfterRenderAsync`**, replace the inline localStorage read with a helper call and subscribe to viewport changes:

```diff
- string storageKey = await GetSidebarStorageKeyAsync();
- string? savedState = await JSRuntime.InvokeAsync<string?>("hexalithAdmin.getLocalStorage", storageKey);
- if (bool.TryParse(savedState, out bool collapsed)) { _sidebarCollapsed = collapsed; }
- else if (IsNarrowTier(storageKey)) { _sidebarCollapsed = true; }
+ await LoadSidebarStateForCurrentTierAsync();
+ await ViewportService.InitializeAsync();
+ ViewportService.OnViewportChanged += OnViewportChanged;
```

4. **Add helper methods**:

```csharp
private async Task LoadSidebarStateForCurrentTierAsync()
{
    string storageKey = await GetSidebarStorageKeyAsync();
    string? savedState = await JSRuntime.InvokeAsync<string?>("hexalithAdmin.getLocalStorage", storageKey);
    if (bool.TryParse(savedState, out bool collapsed))
    {
        _sidebarCollapsed = collapsed;
    }
    else
    {
        // Two states only:
        //   _sidebarCollapsed = false → Normal: 220px, icons + labels (≥1280px default)
        //   _sidebarCollapsed = true  → Compact-text: 140px, labels only, no icons (<1280px default)
        // Ctrl+B toggles between the two regardless of viewport (saved per-tier).
        _sidebarCollapsed = IsNarrowTier(storageKey);
    }
}

private void OnViewportChanged()
{
    _ = InvokeAsync(async () =>
    {
        try
        {
            await LoadSidebarStateForCurrentTierAsync();
            StateHasChanged();
        }
        catch (JSDisconnectedException) { /* circuit gone during resize */ }
        catch (ObjectDisposedException) { /* component disposed mid-resize */ }
    });
}
```

5. **Unsubscribe in `DisposeAsync`**:

```diff
+ ViewportService.OnViewportChanged -= OnViewportChanged;
```

### Patch 3 — `app.css` sidebar styling and banner threshold

**File:** `src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css`

1. **Compact-text mode rules** (replace the old `width:48px` collapsed rule):

```css
/* Compact-text mode: fixed 140px, labels only (icons hidden below). */
.admin-sidebar.collapsed {
    width: 140px;
    min-width: 140px;
}

/* Hide FluentNavItem icons in compact-text mode so labels sit immediately
   after the active-page indicator (the blue bar on the left edge). */
.admin-sidebar.collapsed fluent-nav-item [slot="start"] {
    display: none !important;
}
```

2. **Move the banner from `<960px` to `<1280px`** and remove the conflicting `width: 48px` rule from the same media query:

```css
/* Narrow: <1280px — hide Domain/Snapshot columns, show banner, default to compact-text sidebar.
   Sidebar width is driven by C# (_sidebarCollapsed in MainLayout), not CSS,
   to avoid the two systems fighting each other when the user toggles via Ctrl+B. */
@media (max-width: 1279px) {
    .stat-card-grid {
        grid-template-columns: repeat(2, 1fr);
    }

    .narrow-screen-warning {
        display: block;
    }

    /* ... existing column-hiding rules unchanged ... */
}

/* Minimum: <960px — also hide Tenant column and stack stat cards vertically.
   Banner already shows at <1280px (above), so no banner rule here. */
@media (max-width: 959px) {
    .stat-card-grid {
        grid-template-columns: 1fr;
    }

    .streams-col-tenant {
        display: none !important;
    }
}
```

### Patch 4 — Regression test

**File:** `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TypeCatalogPageTests.cs`

```csharp
[Fact]
public async Task TypeCatalogPage_UpdateUrl_DoesNotNavigate_WhenUserHasLeftTypesPage()
{
    // Regression: when the user navigates away from /types (e.g. clicks a NavMenu link),
    // FluentTabs v5 may still fire ActiveTabIdChanged during teardown. UpdateUrl must
    // refuse to call NavigateTo("/types?...") in that case, otherwise the user is yanked
    // back and the tab oscillates on every external click.
    SetupMockData();

    NavigationManager nav = Services.GetRequiredService<NavigationManager>();
    IRenderedComponent<TypeCatalog> cut = Render<TypeCatalog>();
    cut.WaitForAssertion(() => cut.Markup.ShouldContain("Event Types"), TimeSpan.FromSeconds(5));

    nav.NavigateTo("/streams");

    // Simulate stray ActiveTabIdChanged firing during teardown.
    var activeTabField = typeof(TypeCatalog).GetField("_activeTab", BindingFlags.NonPublic | BindingFlags.Instance)!;
    activeTabField.SetValue(cut.Instance, "commands");

    int navigationCount = 0;
    nav.LocationChanged += (_, _) => navigationCount++;

    var updateUrl = typeof(TypeCatalog).GetMethod("UpdateUrl", BindingFlags.NonPublic | BindingFlags.Instance)!;
    await cut.InvokeAsync(() => { _ = updateUrl.Invoke(cut.Instance, new object?[] { true }); });

    cut.WaitForAssertion(() => navigationCount.ShouldBe(0), TimeSpan.FromMilliseconds(200));
    nav.Uri.ShouldEndWith("/streams");
}
```

---

## Section 5: Implementation Handoff

**Scope:** Minor. Changes are localized to four files in `Admin.UI`, all already implemented and validated by the developer in this session.

**Recipient:** Developer (current branch maintainer) — for commit, PR creation, and review.

**Deliverables:**
1. ✅ Source patches applied on branch `fix/admin-ui-stream-page-bug-bundle`
2. ✅ Regression test passing (22/22 in `TypeCatalogPageTests`)
3. ✅ Full solution build clean (0 warning, 0 error)
4. ✅ Aspire restart validated end-to-end with manual browser test pending sign-off
5. ⏳ **Pending operator sign-off** in browser for the four AC scenarios:
   - **AC-1:** From `/types?tab=aggregates`, click any NavMenu item → leaves cleanly, no tab alternation, no JSDisconnectedException in Aspire trace logs
   - **AC-2:** Resize Chrome <1280px → sidebar transitions to Compact-text 140px, icons hidden, labels left-aligned after blue active indicator, Topology chevron right-aligned, banner appears
   - **AC-3:** Resize Chrome ≥1280px → sidebar restores to Normal 220px with icons + labels, banner disappears
   - **AC-4:** `Ctrl+B` at any viewport → toggle Normal ↔ Compact-text, persisted per-tier in localStorage

**Commit plan** (pending sign-off):

```
fix(admin-ui): types page redirect-back + responsive sidebar 2-state model

* TypeCatalog: guard UpdateUrl against teardown re-navigation when user
  has already left /types. FluentTabs v5 fires ActiveTabIdChanged during
  component dispose, which previously yanked users back and oscillated tabs
  on every external NavMenu click. Adds a regression test that reproduces
  the teardown re-fire scenario.
* MainLayout: subscribe to ViewportService.OnViewportChanged so the
  sidebar reloads its per-tier saved state when the viewport crosses 1280px.
  Replaces the previous one-shot read in OnAfterRenderAsync that left the
  sidebar stuck in its initial state through resizes.
* Sidebar 2-state model: replaces the old expanded/icon-only collapse
  with Normal (220px, icons+labels) and Compact-text (140px, labels only).
  Default at <1280px is Compact-text; at ≥1280px is Normal. Ctrl+B toggles
  between the two regardless of viewport (saved per-tier in localStorage).
* CSS: hide FluentNavItem icons in .collapsed mode via [slot="start"];
  move the "Dashboard optimized for wider screens." banner threshold from
  <960px to <1280px so it appears together with Compact-text mode; remove
  the previous width:48px override that was fighting the C# state.
```

**Out-of-scope follow-ups (logged for future stories):**

1. **Phone-class viewport (<600px)** — the user explicitly excluded this from current scope. Future story should design hamburger toggle + drawer pattern for phone widths.
2. **Sidebar localStorage migration** — instead of the manual F12 reset, a one-shot `hexalith-sidebar-version` key could clear legacy entries on first load. Cost: ~10 lines of JS in `wwwroot/js/admin.js`. Defer until more than ~5 testers are affected.
3. **bUnit test coverage for MainLayout sidebar viewport reactivity** — needs a JS-interop double for `hexalithAdmin.getViewportWidth` and `setLocalStorage`. Worth it once the sidebar grows additional states or thresholds.
4. **`.razor` audit for `.ConfigureAwait(false)` before `StateHasChanged()`** — already noted in the prior bug bundle (`sprint-change-proposal-2026-05-06-admin-ui-stream-page-bug-bundle.md`); reaffirmed here.
