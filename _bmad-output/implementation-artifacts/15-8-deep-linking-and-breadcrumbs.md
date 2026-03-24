# Story 15.8: Deep Linking and Context-Aware Breadcrumbs

Status: done
Size: Small-Medium — ~4 new/modified files, 5 task groups, 13 ACs (5 are verify-only), ~15 tests (~5-8 hours estimated). Core new work: breadcrumb enhancement (AC 1-4), verification of existing deep-linking (AC 5-11), browser nav (AC 12-13).

## Definition of Done

- All 13 ACs verified
- Merge-blocking bUnit tests green (Task 4 blocking tests)
- Recommended bUnit tests green
- Project builds with zero warnings in CI (`dotnet build --configuration Release`)
- No new analyzer suppressions

## Story

As a **developer or DBA using the Hexalith EventStore admin dashboard**,
I want **context-aware breadcrumbs that show meaningful labels (page names, tenant IDs, domain names, aggregate IDs) and every page's filter/view state persisted in shareable URLs with a copy-link button**,
so that **I can share exact investigation contexts with colleagues via URL, navigate the dashboard hierarchy intuitively, and return to any previously visited state by bookmarking or pasting a URL**.

## Acceptance Criteria

1. **Context-aware breadcrumb labels** — Breadcrumb segments display semantic labels instead of raw URL path segments. Top-level pages show their display name (e.g., "Streams" not "streams", "Type Catalog" not "types"). StreamDetail route `/streams/{TenantId}/{Domain}/{AggregateId}` renders breadcrumb: `Home / Streams / {TenantId} / {Domain} / {AggregateId}` where TenantId, Domain, and AggregateId segments use monospace font (CSS class `monospace`). DeadLetters at `/health/dead-letters` renders: `Home / Health / Dead Letters`. Each segment except the last is a clickable link navigating to that level (UX-DR43).

2. **Breadcrumb route mapping** — A static route-to-label dictionary maps known path segments to display names: `streams` → "Streams", `health` → "Health", `projections` → "Projections", `types` → "Type Catalog", `commands` → "Commands", `events` → "Events", `services` → "Services", `tenants` → "Tenants", `settings` → "Settings", `dead-letters` → "Dead Letters". Unknown segments (dynamic parameters like tenant IDs, aggregate IDs) are displayed verbatim in monospace font. The dictionary is a `static IReadOnlyDictionary<string, string>` in `Breadcrumb.razor`'s `@code` block.

3. **Responsive breadcrumb truncation** — At compact viewport (below 1280px), when the breadcrumb has more than 4 segments (Home + 3 others), truncate to show Home + "..." button + last 3 segments. Example: `Home / Streams / tenant-acme / banking / agg-7f3b` truncates to `Home / ... / tenant-acme / banking / agg-7f3b` (preserving tenant context for multi-tenant investigations). Clicking "..." expands to show all segments. At standard and optimal viewports (1280px+), all segments are shown. Truncation state resets on navigation. Use `ViewportService.IsWideViewport` (1280px breakpoint) for the check. The "..." element is a `<button>` with `aria-label="Show full breadcrumb path"`. **Boundary:** A breadcrumb with exactly 4 segments (Home + 3) is NOT truncated — truncation applies only when segment count exceeds 4. [Source: ux-design-specification.md line 1599 — "Breadcrumb truncates to last 3 segments"]

4. **Copy URL button** — A copy-link button appears at the end of the breadcrumb bar (right-aligned). Icon: `Icons.Regular.Size16.Copy` (FluentUI icon). On click, copies the current full URL (including query parameters) to the clipboard via `navigator.clipboard.writeText()` JS interop. On success: `IToastService.ShowSuccess("Link copied to clipboard")`. On failure (JS interop returns `false`): `IToastService.ShowError("Could not copy link")`. Handler must guard against `ObjectDisposedException` (user navigates away during async clipboard call) using `_disposed` flag + try/catch — same pattern as stories 15-5, 15-6, 15-7. `IToastService` is already registered (`FluentToastProvider` in `App.razor`, used in `Projections.razor` and `ProjectionDetailPanel.razor`). The button has `aria-label="Copy page URL to clipboard"` and `title="Copy link"`. The copy button is always visible regardless of viewport size.

5. **URL state for Streams page** — `/streams` persists all filter state in URL query parameters: `?page=N` (pagination), `?status=active|idle|tombstoned` (status filter), `?tenant=<id>` (tenant filter), `?domain=<name>` (domain filter). All parameters are optional. Changing any filter updates the URL via `NavigationManager.NavigateTo(url, forceLoad: false, replace: true)`. Page loads with filters pre-applied when URL parameters are present. All user-provided values are escaped with `Uri.EscapeDataString()`. (Already implemented — verify and preserve.)

6. **URL state for StreamDetail page** — `/streams/{TenantId}/{Domain}/{AggregateId}` persists view state: `?from=<seq>`, `?to=<seq>`, `?type=event|command|query`, `?correlation=<id>`, `?detail=<seq>`, `?view=causation`, `?diff=<from>-<to>`, `?inspect=<seq>`. (Already implemented — verify and preserve.)

7. **URL state for Projections page** — `/projections` persists: `?tenant=<id>`, `?status=running|paused|error|rebuilding`, `?projection=<name>`. (Already implemented — verify and preserve.)

8. **URL state for TypeCatalog page** — `/types` persists: `?tab=events|commands|aggregates`, `?domain=<name>`, `?search=<text>`. (Already implemented — verify and preserve.)

9. **URL state for Health page** — `/health` is a single-view page with no filters. No URL parameters needed. Deep linking at `/health` restores default view. (Already implemented — verify and preserve.)

10. **Cross-page contextual links** — Clickable elements that navigate between pages include filter context in the target URL: (a) Index page "Active Streams" stat card click navigates to `/streams?status=active` (already implemented). (b) Index page "Error Rate" stat card click navigates to `/health` (already implemented). (c) Stream grid rows in Streams page navigate to `/streams/{TenantId}/{Domain}/{AggregateId}` (already implemented). (d) NavMenu topology tree domain click navigates to `/streams?tenant={id}&domain={name}` (already implemented). Verify all cross-page links preserve filter context correctly.

11. **Command palette deep-link entries** — Command palette entries with query parameters navigate correctly: "Event Types" → `/types?tab=events`, "Command Types" → `/types?tab=commands`, "Aggregate Types" → `/types?tab=aggregates`. (Already implemented — verify.)

12. **Accessibility** — Breadcrumb container has `aria-label="Breadcrumb"` and uses `<nav>` element. Separator "/" has `aria-hidden="true"`. Current page segment has `aria-current="page"`. Copy button has `aria-label`. Truncation "..." button has `aria-label`. All breadcrumb links are keyboard-focusable. Focus order: breadcrumb appears before main content in tab order (per UX spec: "Skip to main content → header → sidebar → breadcrumb → content").

13. **Browser navigation** — Back/forward buttons work correctly with deep-linked URLs. Filter changes use `replace: true` to avoid polluting browser history with every filter tweak. Page-level navigation (Streams → StreamDetail) uses default history push (not replace). Breadcrumb link clicks use standard navigation (not replace).

## Tasks / Subtasks

- [x] **Task 1: Enhance Breadcrumb.razor to be context-aware** (AC: 1, 2, 3, 12)
  - [x] 1.1 Add `static IReadOnlyDictionary<string, string>` route label mapping in `@code` block: `{ "streams" → "Streams", "health" → "Health", "projections" → "Projections", "types" → "Type Catalog", "commands" → "Commands", "events" → "Events", "services" → "Services", "tenants" → "Tenants", "settings" → "Settings", "dead-letters" → "Dead Letters" }`.
  - [x] 1.2 Update `UpdateSegments()`: for each path segment, check the route label dictionary first. If found, use the display name and do NOT apply monospace CSS. If not found (dynamic segment like tenant ID, aggregate ID), use the URL-decoded raw value verbatim (`Uri.UnescapeDataString(part)`) and add CSS class `monospace`. When building the href for each segment, preserve the original URL-encoded path segments (do NOT double-encode). Dynamic segments containing special characters (e.g., `/`, `+`) will be URL-encoded in the route — the display label should show the decoded value, the `href` should use the original encoded value.
  - [x] 1.3 Handle multi-segment known paths: `dead-letters` appears as a single path segment in `/health/dead-letters`. The dictionary lookup should match the segment `dead-letters` directly.
  - [x] 1.4 Wrap breadcrumb in `<nav>` element (currently a `<div>`). Keep `aria-label="Breadcrumb"`.
  - [x] 1.5 Inject `ViewportService`. Add responsive truncation: if `!ViewportService.IsWideViewport` and segments count > 4 (Home + 3 others), show only Home + "..." button + last 3 segments. This preserves tenant context in multi-tenant investigations (e.g., `Home / ... / tenant-acme / banking / agg-7f3b`). "..." is a `<button>` with `aria-label="Show full breadcrumb path"`. Clicking toggles `_showFullPath` boolean to reveal all segments. `_showFullPath` resets to `false` on `LocationChanged`. **Boundary:** exactly 4 segments (Home + 3) — do NOT truncate. **Note:** `ViewportService` must be initialized before use — call `await ViewportService.InitializeAsync()` in `OnAfterRenderAsync(firstRender)` (not `OnInitialized`, since JS interop is unavailable during prerender).
  - [x] 1.6 Subscribe to `ViewportService.OnViewportChanged` event (confirmed: `event Action?` at `ViewportService.cs:26`) to re-render when viewport crosses 1280px threshold. Handler: `ViewportService.OnViewportChanged += OnViewportChanged;` where `private void OnViewportChanged() { _ = InvokeAsync(StateHasChanged); }`. In `Dispose()`, unsubscribe: `ViewportService.OnViewportChanged -= OnViewportChanged;` (component already implements `IDisposable` for `LocationChanged`).
  - [x] 1.7 **Checkpoint**: Breadcrumb shows semantic labels, monospace for dynamic segments, truncation on narrow viewports.

- [x] **Task 2: Add copy URL button** (AC: 4, 12)
  - [x] 2.1 Add a copy button at the end of the breadcrumb bar. Use `<button>` with FluentUI `Icons.Regular.Size16.Copy` icon (or inline SVG if FluentUI icon component is too heavy for breadcrumb). Button styled as icon-only, right-aligned within the breadcrumb bar via `display: flex; justify-content: space-between;` on the outer container.
  - [x] 2.2 Add JS interop method `hexalithAdmin.copyToClipboard(text)` in `wwwroot/js/interop.js` that calls `navigator.clipboard.writeText(text)`. Return a boolean indicating success.
  - [x] 2.3 On click handler: wrap in `_disposed` guard + try/catch `ObjectDisposedException` (user may navigate away during async clipboard call — same pattern as 15-5/15-6/15-7). `bool success = await JSRuntime.InvokeAsync<bool>("hexalithAdmin.copyToClipboard", NavigationManager.Uri)`. On success: `ToastService.ShowSuccess("Link copied to clipboard")`. On failure: `ToastService.ShowError("Could not copy link")`. No manual animation needed — FluentUI toast handles display and auto-dismiss.
  - [x] 2.6 Add `private bool _disposed;` field. Set to `true` in `Dispose()`. Check `if (_disposed) return;` at the top of the copy handler. Wrap toast calls in `try { ... } catch (ObjectDisposedException) { }`.
  - [x] 2.4 Add `aria-label="Copy page URL to clipboard"` and `title="Copy link"` to the button.
  - [x] 2.5 Inject `IJSRuntime`, `IToastService` (in addition to existing `NavigationManager`).
  - [x] 2.6 **Checkpoint**: Copy button visible, clicking copies URL, transient feedback shown.

- [x] **Task 3: Verify and consolidate deep-linking across all pages** (AC: 5, 6, 7, 8, 9, 10, 11, 13)
  - [x] 3.1 Audit all implemented pages (Streams, StreamDetail, Projections, TypeCatalog, Health, Index) to verify URL state is correctly persisted and restored. This is a verification task — these pages already implement deep-linking. Document any issues found.
  - [x] 3.2 Verify cross-page contextual links: Index stat card clicks, NavMenu topology tree navigation, stream row clicks to StreamDetail. Ensure all pass filter context via URL parameters.
  - [x] 3.3 Verify command palette entries with query params (`/types?tab=events`, etc.) navigate correctly.
  - [x] 3.4 Verify browser back/forward works correctly with `replace: true` for filter changes and standard push for page navigation.
  - [x] 3.5 Fix any issues found during audit. If no issues found, no changes needed.
  - [x] 3.6 **Checkpoint**: All deep-linking verified working end-to-end.

- [x] **Task 4: Unit tests (bUnit)** (AC: 1-4, 12)
  - **Mock dependencies** — use `AdminUITestContext` base class
  - **Merge-blocking tests** (must pass):
  - [x] 4.1 Test `Breadcrumb` renders semantic label "Streams" for `/streams` route instead of raw "streams" (AC: 1, 2)
  - [x] 4.2 Test `Breadcrumb` renders "Type Catalog" for `/types` route (multi-word label) (AC: 2)
  - [x] 4.3 Test `Breadcrumb` renders dynamic segments in monospace for `/streams/{TenantId}/{Domain}/{AggregateId}` (AC: 1)
  - [x] 4.4 Test `Breadcrumb` renders `Home / Health / Dead Letters` for `/health/dead-letters` (AC: 1, 2)
  - [x] 4.5 Test `Breadcrumb` renders `<nav>` element with `aria-label="Breadcrumb"` (AC: 12)
  - [x] 4.6 Test `Breadcrumb` final segment has `aria-current="page"` and is not a link (AC: 12)
  - [x] 4.7 Test copy button has `aria-label="Copy page URL to clipboard"` (AC: 4, 12)
  - [x] 4.8 Test `Breadcrumb` is not rendered on home page `/` (AC: 1)
  - **Recommended tests**:
  - [x] 4.9 Test truncation: when viewport is narrow (mock `ViewportService.IsWideViewport = false`) and path has 5+ segments, breadcrumb shows "..." button (AC: 3)
  - [x] 4.10 Test truncation "..." button expands to show all segments when clicked (AC: 3)
  - [x] 4.11 Test each intermediate breadcrumb segment is a clickable `<a>` link with correct `href` (AC: 1)
  - [x] 4.12 Test separator "/" has `aria-hidden="true"` (AC: 12)
  - [x] 4.13 Test copy button invokes JS interop `hexalithAdmin.copyToClipboard` with current URL (AC: 4)
  - [x] 4.14 Test breadcrumb truncation resets to collapsed state on navigation change (AC: 3)
  - [x] 4.15 Test unknown path segments (dynamic route params) do not appear in route label dictionary and render verbatim (AC: 2)

- [x] **Task 5: CSS and final polish** (AC: 3, 4, 12)
  - [x] 5.1 Update `wwwroot/css/app.css` breadcrumb styles: add flex layout with `justify-content: space-between` to position copy button at right. Add `.breadcrumb-copy-btn` class for the copy button (icon-only, no border, hover highlight, `padding: 2px 4px`, `border-radius: var(--layer-corner-radius)`). Add `.breadcrumb-truncation-btn` for "..." button (no border, cursor pointer, same font size, hover color change).
  - [x] 5.2 Add `.breadcrumb-segments` wrapper class with `display: flex; align-items: center; flex-wrap: nowrap; gap: 0; overflow: hidden; text-overflow: ellipsis;` to contain segment links and separators. Use `nowrap` (not `wrap`) — truncation via AC 3 is the overflow strategy, not line wrapping. On wide viewports where truncation is off, `overflow: hidden` clips extremely long breadcrumbs gracefully.
  - [x] 5.3 Ensure breadcrumb does not cause horizontal scrolling at any viewport width (UX spec requirement: page chrome must never scroll horizontally). Use `overflow: hidden; text-overflow: ellipsis; white-space: nowrap;` on individual long segments if needed.
  - [x] 5.4 No CSS animation needed for copy feedback — `IToastService.ShowSuccess()` handles the FluentUI toast display and auto-dismiss.
  - [x] 5.5 **Checkpoint**: All ACs pass, zero warnings, breadcrumb fully functional.

## Dev Notes

### Architecture Compliance

- **UX-DR42**: Deep linking — every view, stream, event, projection, and filter has a unique shareable URL. This story verifies all implemented pages comply and adds the copy-link button for sharing.
- **UX-DR43**: Context-aware breadcrumbs — investigation path shown as navigation trail, shareable for incident collaboration. This story replaces the generic path-based breadcrumb with semantic labels.
- **ADR-P4**: Admin.UI communicates exclusively via HTTP REST API to Admin.Server. No new API calls in this story — breadcrumb and copy-link are client-side-only.
- **SEC-5**: No event payload data in this story — breadcrumb and URL operations are navigation metadata only.
- **NFR45**: Supports concurrent users — no shared mutable state. Breadcrumb state is component-scoped.

### Scope — Enhancement of Existing Navigation Infrastructure

This story does NOT add deep-linking to placeholder/stub pages (Commands, Events, Services, Tenants, Settings). Those pages have no filter UI yet — deep-linking will be added when each page gets its real implementation. This story:
1. Enhances the existing `Breadcrumb.razor` component to be context-aware (UX-DR43)
2. Adds a copy URL button for sharing (UX-DR42)
3. Adds responsive breadcrumb truncation (UX spec compact tier requirement)
4. Verifies existing deep-linking on all implemented pages works correctly

### Existing Code to Reuse (DO NOT Recreate)

| What | Where | How |
|------|-------|-----|
| `Breadcrumb.razor` | `src/Hexalith.EventStore.Admin.UI/Layout/Breadcrumb.razor` | MODIFY — enhance with route mapping, truncation, copy button |
| `MainLayout.razor` | `src/Hexalith.EventStore.Admin.UI/Layout/MainLayout.razor` | Already renders `<Breadcrumb />` conditionally. No changes needed. |
| `ViewportService` | `src/Hexalith.EventStore.Admin.UI/Services/ViewportService.cs` | `IsWideViewport` boolean + `OnViewportChanged` event for responsive truncation |
| `interop.js` | `src/Hexalith.EventStore.Admin.UI/wwwroot/js/interop.js` | ADD `copyToClipboard(text)` function |
| `app.css` | `src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css` | MODIFY — update breadcrumb styles for flex layout, copy button, truncation |
| `AdminUITestContext` | `tests/Hexalith.EventStore.Admin.UI.Tests/AdminUITestContext.cs` | Base test context with FluentUI, mock services. JSInterop.Mode is Loose. `AddFluentUIComponents()` registers `IToastService`. |
| `CommandPaletteCatalog.cs` | `src/Hexalith.EventStore.Admin.UI/Components/CommandPaletteCatalog.cs` | Already has deep-link entries. No changes needed. |

### Unknown Route Fallback Behavior

Routes not in the dictionary AND not recognizable as dynamic parameters (e.g., a user manually navigates to `/foo/bar`) are displayed verbatim in monospace, same as dynamic segments. This is acceptable graceful degradation — the breadcrumb never breaks, it just shows raw segment values for unrecognized paths. No special handling needed.

### Known Limitation: Breadcrumb Does Not Reflect Query Parameters

The breadcrumb reflects URL **path** hierarchy only, not query parameters. This means `/streams?tenant=acme&domain=banking` and `/streams` show the same breadcrumb: `Home / Streams`. The tenant/domain filter context is NOT visible in the breadcrumb. This is by design — breadcrumbs navigate path levels, and clicking "Streams" navigates to `/streams` (unfiltered), not `/streams?tenant=acme`. The copy URL button compensates: it copies the full URL including query parameters, so the shared link preserves filter state even though the breadcrumb does not display it.

### Defensive Max Segment Cap

If a URL contains an unusually large number of path segments (10+), the breadcrumb renders all of them on wide viewports. This could cause visual clutter but not layout breakage (CSS `overflow: hidden` clips). No hard cap is needed for v1 — the app's deepest route is 4 levels (`/streams/{tenant}/{domain}/{agg}`). If future epics add deeper routes, a max cap can be added then.

### Prerender Flash for Truncation

On narrow viewports, the breadcrumb may briefly show all segments before truncating. This happens because `ViewportService.InitializeAsync()` runs in `OnAfterRenderAsync(firstRender)` — during Blazor Server prerender, `IsWideViewport` defaults to `true` (prerender-safe). After JS interop resolves (~100ms), truncation kicks in. This is standard Blazor Server behavior and matches the pattern used by Projections and StreamDetail pages. No mitigation needed.

### Current Breadcrumb Implementation (What to Change)

The current `Breadcrumb.razor` (70 lines) is generic:
- Splits URL path by `/`, title-cases each segment, builds clickable links
- All segments use monospace font (CSS class `monospace` on current segment)
- No route awareness — `/types` shows "Types" instead of "Type Catalog"
- No responsive truncation
- No copy button
- Uses `<div>` instead of `<nav>` for container

### Deep-Linking Patterns Already Established

Two URL parameter parsing approaches exist in the codebase:

**Approach 1: Manual `HttpUtility.ParseQueryString`** (Streams, Projections, TypeCatalog):
```csharp
private void ReadUrlParameters()
{
    Uri uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
    string? page = query["page"];
    // ...
}
```

**Approach 2: `[SupplyParameterFromQuery]` attributes** (StreamDetail):
```csharp
[SupplyParameterFromQuery(Name = "from")]
public long? QueryFrom { get; set; }
```

Both approaches are valid. Do NOT refactor existing pages to standardize — just verify they work.

### URL Update Pattern (All Pages Use This)

```csharp
private void UpdateUrl()
{
    List<string> queryParams = [];
    if (_currentFilter != null) queryParams.Add($"filter={Uri.EscapeDataString(_currentFilter)}");
    // ...
    string url = queryParams.Count > 0 ? $"{path}?{string.Join('&', queryParams)}" : path;
    NavigationManager.NavigateTo(url, forceLoad: false, replace: true);
}
```

Key: `replace: true` for filter changes (no history entry per filter tweak).

### JS Interop for Clipboard — Required Pattern

Add to `wwwroot/js/interop.js` inside the `window.hexalithAdmin` namespace:

```javascript
copyToClipboard: async function(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch (e) {
        return false;
    }
}
```

**Note:** `navigator.clipboard.writeText()` requires HTTPS or localhost. In the Aspire dev environment, this is always true. No fallback (`document.execCommand('copy')`) needed — it's deprecated and the admin dashboard targets modern browsers.

### ViewportService Integration Pattern

`ViewportService` (confirmed at `Services/ViewportService.cs`) exposes:
- `bool IsWideViewport` — true when viewport >= 1280px
- `event Action? OnViewportChanged` — fires when viewport crosses 1280px threshold
- `Task InitializeAsync()` — must be called in `OnAfterRenderAsync(firstRender)` (requires JS interop, unavailable during prerender)

```csharp
@inject ViewportService ViewportService
@implements IDisposable

@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await ViewportService.InitializeAsync();
            ViewportService.OnViewportChanged += OnViewportChanged;
            StateHasChanged();
        }
    }

    private void OnViewportChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        ViewportService.OnViewportChanged -= OnViewportChanged;
        NavigationManager.LocationChanged -= OnLocationChanged;
    }
}
```

### IToastService — Already Registered

`FluentToastProvider` is declared in `Components/App.razor:21`. `IToastService` is injected and used in `Projections.razor` and `ProjectionDetailPanel.razor`. The breadcrumb copy button should use:

```csharp
@inject IToastService ToastService

private bool _disposed;

// In copy handler:
private async Task CopyUrlAsync()
{
    if (_disposed) return;
    try
    {
        bool success = await JSRuntime.InvokeAsync<bool>("hexalithAdmin.copyToClipboard", NavigationManager.Uri);
        if (success)
        {
            ToastService.ShowSuccess("Link copied to clipboard");
        }
        else
        {
            ToastService.ShowError("Could not copy link");
        }
    }
    catch (ObjectDisposedException) { }
}

// In Dispose():
public void Dispose()
{
    _disposed = true;
    ViewportService.OnViewportChanged -= OnViewportChanged;
    NavigationManager.LocationChanged -= OnLocationChanged;
}
```

No manual CSS animation or `_showCopied` state needed — FluentUI toast handles display and auto-dismiss. The `_disposed` guard prevents `ObjectDisposedException` when user navigates away during async clipboard call (same pattern as 15-5/15-6/15-7).

### Breadcrumb CSS — Existing Styles in app.css (Lines 102-124)

```css
.admin-breadcrumb { padding: 4px 16px; font-size: 13px; }
.admin-breadcrumb .separator { margin: 0 6px; color: var(--neutral-foreground-hint); }
.admin-breadcrumb a { color: var(--accent-fill-rest); text-decoration: none; }
.admin-breadcrumb a:hover { text-decoration: underline; }
.admin-breadcrumb .current { color: var(--neutral-foreground-rest); }
```

These need updating to:
- Add `display: flex; align-items: center; justify-content: space-between;` on `.admin-breadcrumb`
- Add breadcrumb segments wrapper (left side) and copy button (right side)
- Add truncation button styles

### bUnit Test Pattern

Follow `HealthPageTests` / `ProjectionsPageTests` pattern:
- Extend `AdminUITestContext`
- Use `JSInterop.Mode = Loose` (already set in base class)
- For breadcrumb tests: set `NavigationManager` URL before rendering, then assert markup
- For copy button: verify JSInterop invocation with `JSInterop.VerifyInvoke("hexalithAdmin.copyToClipboard")`

To test breadcrumb with a specific URL:
```csharp
using var ctx = new AdminUITestContext();
ctx.Services.GetRequiredService<NavigationManager>()
    .NavigateTo("/streams/tenant-acme/banking/agg-123");
var cut = ctx.Render<Breadcrumb>();
cut.Markup.ShouldContain("Streams");
cut.Markup.ShouldContain("tenant-acme");
```

### Previous Story Intelligence (15-7)

- `DashboardRefreshService.OnDataChanged` subscription pattern is proven and stable
- bUnit test pattern with `AdminUITestContext` base class provides all needed mocks
- JSInterop in tests uses `Mode = Loose` — new JS methods work without explicit setup
- FluentUI Blazor v4.13.2 icon components: use `<FluentIcon Value="@(new Icons.Regular.Size16.Copy())" />` syntax
- All tests pass: 169/169 after story 15-7 — no regressions allowed

### Project Structure Notes

Files to create/modify:
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Layout/Breadcrumb.razor` (context-aware labels, truncation, copy button)
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/wwwroot/js/interop.js` (add copyToClipboard)
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css` (breadcrumb flex layout, copy button, truncation styles)
- **CREATE**: `tests/Hexalith.EventStore.Admin.UI.Tests/Layout/BreadcrumbTests.cs` (bUnit tests)

No new API clients, no new models, no server-side changes. No changes to MainLayout.razor.

### References

- [Source: ux-design-specification.md — UX-DR42: Deep Linking]
- [Source: ux-design-specification.md — UX-DR43: Context-Aware Breadcrumbs]
- [Source: ux-design-specification.md — Breadcrumb Trail section (lines 1459-1471)]
- [Source: ux-design-specification.md — Responsive Breakpoint Implementation (lines 1581-1628)]
- [Source: ux-design-specification.md — Pre-Filtered Navigation (lines 1473-1480)]
- [Source: ux-design-specification.md — WCAG 1.3.2: Breadcrumb before page body]
- [Source: ux-design-specification.md — WCAG 2.4.3: Focus order includes breadcrumb]
- [Source: Admin.UI/Layout/Breadcrumb.razor — Current generic implementation]
- [Source: Admin.UI/Layout/MainLayout.razor:31-34 — Breadcrumb conditional rendering]
- [Source: Admin.UI/wwwroot/css/app.css:102-124 — Current breadcrumb CSS]
- [Source: Admin.UI/wwwroot/js/interop.js — Existing JS interop namespace]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

None — no debug issues encountered.

### Completion Notes List

- Enhanced `Breadcrumb.razor` from generic path-based breadcrumb to context-aware component with semantic route labels, monospace dynamic segments, responsive truncation, and copy URL button.
- Added `copyToClipboard` JS interop function to `interop.js`.
- Updated `app.css` with flex layout for breadcrumb bar, copy button styles, and truncation button styles.
- Created 15 bUnit tests (8 merge-blocking + 7 recommended) covering all breadcrumb ACs.
- Deep-linking audit of all 8 pages/components confirmed correct URL state management — no issues found, no changes needed.
- All 184 Admin.UI tests pass (169 existing + 15 new) — zero regressions.
- Build: 0 warnings, 0 errors.

### File List

- `src/Hexalith.EventStore.Admin.UI/Layout/Breadcrumb.razor` — MODIFIED: context-aware labels, truncation, copy button, `<nav>` element, viewport service integration
- `src/Hexalith.EventStore.Admin.UI/wwwroot/js/interop.js` — MODIFIED: added `copyToClipboard` function
- `src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css` — MODIFIED: breadcrumb flex layout, copy button styles, truncation button styles
- `tests/Hexalith.EventStore.Admin.UI.Tests/Layout/BreadcrumbTests.cs` — CREATED: 15 bUnit tests for breadcrumb component
