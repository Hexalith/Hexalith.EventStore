# Story 16.1: Storage Growth Analyzer with Treemap

Status: done
Size: Medium-Large — ~9 new/modified files, 6 task groups, 16 ACs, ~22 tests (~10-16 hours estimated). Core new work: AdminStorageApiClient (Task 1), Storage page with stat cards + hot streams grid (Task 2), treemap SVG component with extracted layout algorithm (Task 3), deep linking + breadcrumb (Task 4), bUnit tests (Task 5), DI + nav registration (Task 6).

## Definition of Done

- All 16 ACs verified
- Merge-blocking bUnit tests green (Task 5 blocking tests)
- Recommended bUnit tests green
- Project builds with zero warnings in CI (`dotnet build --configuration Release`)
- No new analyzer suppressions

## Story

As a **database administrator (Maria) using the Hexalith EventStore admin dashboard**,
I want **a storage growth analyzer page that shows per-tenant storage usage, daily growth rates, hot streams ranked by size, and an interactive treemap visualization breaking down storage by aggregate type**,
so that **I can identify storage-hungry aggregate types, spot streams without snapshots, project capacity trends, and prioritize compaction or snapshot policies — all within 2 minutes without developer involvement**.

## Acceptance Criteria

1. **Storage overview stat cards** — The `/storage` page displays four stat cards in a responsive grid: (a) "Total Events" showing `StorageOverview.TotalEventCount` formatted with `N0`, (b) "Total Storage" showing `StorageOverview.TotalSizeBytes` formatted as human-readable bytes (KB/MB/GB); when null AND all tenant `SizeBytes` are also null (backend globally doesn't support size queries per NFR44), replace this card with "Total Streams" showing the sum of all `TenantBreakdown` stream counts — an "N/A" card wastes valuable dashboard real estate, (c) "Tenants" showing the count of `TenantBreakdown` entries, (d) "Avg Growth/Day" showing the mean of all non-null `GrowthRatePerDay` values formatted as `N0` events/day or "N/A" when no growth data available. Stat cards use the `StatCard` shared component. Loading state shows 4 `SkeletonCard` placeholders.

2. **Tenant storage breakdown table** — Below the stat cards, a `FluentDataGrid` lists all tenants from `StorageOverview.TenantBreakdown` with columns: Tenant ID (monospace), Event Count (right-aligned, `N0`), Size (human-readable bytes or "N/A"), Growth Rate (events/day, `N0`, or "N/A"), and a Growth Indicator column showing a green/amber/red badge based on growth rate thresholds (green: < 1,000/day, amber: 1,000-10,000/day, red: > 10,000/day). These are v1 defaults — thresholds are defined as `const` fields in `Storage.razor`'s `@code` block for easy adjustment in future stories if Maria's production data reveals different norms. Clicking a tenant row filters the hot streams grid and treemap to that tenant. The grid is sortable by all numeric columns, with **default sort by Growth Rate descending** (fastest-growing tenant first — most urgent for Maria's morning check).

3. **Hot streams grid** — A `FluentDataGrid` lists the top 100 streams from `GetHotStreamsAsync` with columns: Tenant ID (monospace), Domain, Aggregate Type, Aggregate ID (monospace, truncated to 12 chars with tooltip for full value), Event Count (right-aligned, `N0`), Size (human-readable bytes or "N/A"), Snapshot (green checkmark if `HasSnapshot`, red cross if not), Snapshot Age (human-readable duration or "—" when no snapshot). Grid is sortable by Event Count and Size, with **default sort by Event Count descending** (largest streams first — most actionable for Maria). Rows without snapshots and with > 1,000 events display a warning icon with tooltip "No snapshot — consider configuring auto-snapshot policy". The Aggregate ID column renders as a clickable `<a>` link navigating to `/streams/{TenantId}/{Domain}/{AggregateId}` — allowing drill-down into the stream's event timeline from the hot streams grid. When a tenant is selected in the breakdown table, the grid is filtered to that tenant.

4. **Treemap visualization** — An interactive SVG treemap renders the storage breakdown by aggregate type. Hierarchy: top level = tenants (when no tenant filter), second level = domains, leaf level = aggregate types. Each rectangle's area is proportional to its storage metric: prefer `SizeBytes` when ALL items in the current view have non-null size data (more meaningful for storage analysis); fall back to `EventCount` when any item has null size (NFR44). Colors: distinct hue per domain using a deterministic hash (FNV-1a or simple character sum — NOT `string.GetHashCode()` which is randomized per process in .NET) selecting from a 10-color palette. This ensures the same domain always gets the same color across page reloads. Each rectangle displays the aggregate type label (truncated if too small) and event count. Hovering shows a tooltip with full details: tenant, domain, aggregate type, event count, size, snapshot status. Clicking a treemap rectangle filters the hot streams grid to that aggregate type. **Tenant drill-down behavior:** When a tenant is selected (via tenant table row click or filter), the treemap completely re-groups its data — it replaces the tenant-level view with that tenant's internal structure (domain → aggregate type hierarchy). This is a full data transformation, not merely a visual highlight. The treemap renders as pure SVG — no external charting library required.

5. **Treemap responsive sizing** — The treemap SVG has a default aspect ratio of 16:9 and fills the available container width. On compact viewport (< 1280px), the treemap collapses to a simpler bar chart view showing the top 10 aggregate types by event count, since treemap rectangles become too small to read. The treemap section has a heading "Storage Distribution" with a toggle button ("Treemap" / "Bar Chart") to manually switch views regardless of viewport.

6. **Tenant filter** — A `FluentTextField` with `@bind-Value` and debounced input (300ms via `System.Threading.Timer`) allows filtering by tenant ID prefix. Clearing the filter shows all tenants. The filter applies to the tenant breakdown table, hot streams grid, and treemap simultaneously. Filter state persists in URL query parameter `?tenant=<id>`. **Blazor Server threading note:** The `Timer` callback runs on a thread pool thread, not the Blazor sync context. The callback MUST wrap all state mutations and `StateHasChanged()` inside `InvokeAsync(() => { ... })` to avoid cross-thread rendering exceptions.

7. **URL state persistence** — The `/storage` page persists filter state in URL query parameters: `?tenant=<id>` (tenant filter), `?type=<aggregateType>` (treemap selection), `?sort=<column>&dir=asc|desc` (grid sort). All parameters are optional. Changing any filter updates the URL via `NavigationManager.NavigateTo(url, forceLoad: false, replace: true)`. Page loads with filters pre-applied from URL parameters. All user-provided values escaped with `Uri.EscapeDataString()`.

8. **Breadcrumb integration** — The breadcrumb route label dictionary in `Breadcrumb.razor` includes `"storage" -> "Storage"`. Navigating to `/storage` renders breadcrumb: `Home / Storage`. No new dynamic segments for this page.

9. **Data loading and refresh** — Initial data loads in `OnInitializedAsync` via `AdminStorageApiClient`. The page uses **manual refresh only** — a "Refresh" button in the page header triggers a full data reload (overview + hot streams). No automatic polling timer. Rationale: storage metrics change slowly (hourly, not per-second), and `DashboardRefreshService` is purpose-built for dashboard index health/streams data — do NOT subscribe to its `OnDataChanged` event, as it does not fetch storage data. Loading state, stale data indicator, and error banner follow the same pattern as `Health.razor`.

10. **Error handling and graceful degradation** — When `AdminStorageApiClient` returns empty data or throws `ServiceUnavailableException`, the page shows `IssueBanner` with "Unable to load storage data" and a retry button. When the server returns data with `TotalSizeBytes = null` (state store doesn't support size queries), all size-related columns show "N/A" instead of 0. The treemap falls back to event count proportions. Previously loaded data is cached and shown with a stale indicator during refresh failures.

11. **Admin role enforcement** — The storage page is visible to all authenticated users (`ReadOnly` minimum). Operator-only actions (compact, snapshot) are NOT in this story — they belong to stories 16-2 and 16-3. This page is read-only.

12. **Accessibility** — The treemap SVG includes `role="img"` and `aria-label="Storage distribution treemap"`. Each rectangle has `aria-label` with aggregate type and event count. A hidden data table equivalent of the treemap is rendered for screen readers (`<table>` with `class="sr-only"`). The stat cards, grids, and filter input follow existing accessibility patterns. Page heading `<h1>Storage</h1>` is the first focusable element.

13. **Performance** — Initial page render within 2 seconds (NFR41). Treemap computation (squarified layout algorithm) runs server-side (this is Blazor Server, not WASM) and the resulting SVG is serialized to the browser via SignalR. For up to 500 aggregate types this is fine (~1,500 SVG elements). For > 500 types, group the smallest types into an "Other" bucket to cap SVG complexity. Memoize treemap layout output — only recompute when input data or filter changes, not on every `StateHasChanged()` cycle. Hot streams grid uses server-side pagination (default 100, hardcoded — no client-side pagination needed for v1).

14. **Navigation entry** — The storage page appears in `NavMenu.razor` between "Tenants" and "Settings", with a `Icons.Regular.Size20.Database` icon (or `HardDrive` if Database doesn't exist). Visible to all authenticated users. Additionally, add a "Storage" entry to `CommandPaletteCatalog.cs` navigating to `/storage` — every page in the admin UI must be reachable via Ctrl+K command palette.

15. **Empty state** — When `StorageOverview.TenantBreakdown` is empty (no tenants) or when `GetHotStreamsAsync` returns zero streams, the respective sections show the `EmptyState` component. Tenant table empty: title "No storage data available", description "No tenants found. Storage data will appear once events are persisted." Hot streams empty: title "No streams found", description "No event streams match the current filter." The treemap section is hidden when there is no data to visualize.

16. **Snapshot risk summary** — Above the hot streams grid, an informational banner summarizes at-risk streams: "{N} streams have > 1,000 events without snapshots." The banner uses `FluentBadge` with amber/warning severity and is only shown when at-risk streams exist in the current view. This gives Maria a quick "aha moment" without needing operator permissions (Journey 8 proactive advisor pattern). The banner is hidden when all streams have snapshots or when the count is zero.

## Tasks / Subtasks

- [x] **Task 1: Create AdminStorageApiClient** (AC: 9, 10)
  - [x] 1.1 Create `src/Hexalith.EventStore.Admin.UI/Services/AdminStorageApiClient.cs` following `AdminStreamApiClient` pattern: constructor takes `IHttpClientFactory` + `ILogger<AdminStorageApiClient>`, uses named client `"AdminApi"`.
  - [x] 1.2 Add method `GetStorageOverviewAsync(string? tenantId, CancellationToken ct)` → calls `GET api/v1/admin/storage/overview?tenantId={id}`, returns `StorageOverview`. Return empty `StorageOverview(0, null, [])` on error.
  - [x] 1.3 Add method `GetHotStreamsAsync(string? tenantId, int count = 100, CancellationToken ct)` → calls `GET api/v1/admin/storage/hot-streams?tenantId={id}&count={count}`, returns `IReadOnlyList<StreamStorageInfo>`. Return empty list on error.
  - [x] 1.4 Do NOT add `GetSnapshotPoliciesAsync` — no AC in this story consumes snapshot policy data. That method belongs in story 16-2 (Snapshot Management). Keep the client focused on what's needed now.
  - [x] 1.5 Error handling: use same `HandleErrorStatus(response)` pattern from `AdminStreamApiClient` — propagate `UnauthorizedAccessException`, `ForbiddenAccessException`, `ServiceUnavailableException`, catch all others and log + return empty defaults. Mark all public methods `virtual` for test mocking.
  - [x] 1.6 Register `AdminStorageApiClient` as scoped in `Program.cs`: `builder.Services.AddScoped<AdminStorageApiClient>();`
  - [x] 1.7 **Checkpoint**: Client builds, methods callable, errors handled gracefully.

- [x] **Task 2: Create Storage.razor page with stat cards, tenant table, hot streams grid** (AC: 1, 2, 3, 6, 7, 9, 10, 11, 12, 15, 16)
  - [x] 2.1 Create `src/Hexalith.EventStore.Admin.UI/Pages/Storage.razor` with `@page "/storage"`. Inject: `AdminStorageApiClient`, `ViewportService`, `NavigationManager`. Do NOT inject `DashboardRefreshService` — this page uses manual refresh only.
  - [x] 2.2 Implement `OnInitializedAsync`: load `StorageOverview` and `IReadOnlyList<StreamStorageInfo>` from `AdminStorageApiClient`. Parse URL query parameters for initial filter state (`?tenant=`, `?type=`, `?sort=`, `?dir=`).
  - [x] 2.3 Render 4 stat cards in a `FluentGrid` (xs=6, sm=6, md=3): Total Events, Total Storage, Tenants count, Avg Growth/Day. Show `SkeletonCard` during loading. Format sizes with helper method `FormatBytes(long? bytes)` → "1.2 GB", "450 MB", or "N/A".
  - [x] 2.4 Render tenant breakdown `FluentDataGrid` with sortable columns. Add growth rate badge rendering: `GetGrowthSeverity(double? rate)` returns "success"/"warning"/"error". Implement row click handler to set tenant filter.
  - [x] 2.5 Render hot streams `FluentDataGrid` with sortable columns. Truncate aggregate IDs to 12 chars with `title` attribute for full value. Show snapshot warning icon for streams with `!HasSnapshot && EventCount > 1000`.
  - [x] 2.6 Add tenant filter `FluentTextField` with debounced input. Timer fires after 300ms of no input. On filter change: update URL, filter tenant table, pass tenant to hot streams grid, update treemap.
  - [x] 2.7 Add URL state management: `ReadUrlParameters()` on init, `UpdateUrl()` on filter change using `replace: true`.
  - [x] 2.8 Add manual "Refresh" button in page header (same style as Health.razor). On click, calls `LoadDataAsync()` to reload all data. No auto-polling — storage metrics change slowly.
  - [x] 2.9 Add `IssueBanner` for error state, stale data indicator, `IAsyncDisposable` cleanup.
  - [x] 2.10 Add `EmptyState` component for empty tenant breakdown and empty hot streams sections (AC: 15).
  - [x] 2.11 Add snapshot risk summary banner above hot streams grid: count streams where `!HasSnapshot && EventCount > 1000`, show amber badge with "{N} streams have > 1,000 events without snapshots" (AC: 16). Hidden when count is zero.
  - [x] 2.12 **Checkpoint**: Page loads, stat cards show, grids render, empty states work, risk banner shows, filtering works, URL state persists.

- [x] **Task 3: Create StorageTreemap component** (AC: 4, 5, 12, 13)
  - [x] 3.1 Create `src/Hexalith.EventStore.Admin.UI/Components/StorageTreemap.razor`. Parameters: `IReadOnlyList<StreamStorageInfo> Data`, `string? SelectedTenant`, `string? SelectedAggregateType`, `EventCallback<string> OnAggregateTypeSelected`.
  - [x] 3.2 Extract squarified treemap layout algorithm to a separate static helper class `src/Hexalith.EventStore.Admin.UI/Components/TreemapLayoutEngine.cs` (NOT in the Razor `@code` block). This makes the algorithm independently unit-testable as a pure function. Input: list of `(string Label, long Value)` items + bounding rectangle (x, y, w, h). Output: list of `(string Label, long Value, double X, double Y, double Width, double Height)` rectangles. Algorithm: sort items descending by value, recursively split into rows maintaining aspect ratio closest to 1.0. The component calls `TreemapLayoutEngine.ComputeLayout(items, 0, 0, 1600, 900)` and memoizes the result — only recompute when input data or filter changes.
  - [x] 3.3 Build treemap data from `StreamStorageInfo`: group by aggregate type, sum values per group. **Data selection:** if ALL items in the current view have non-null `SizeBytes`, use `SizeBytes` as the treemap area metric (more meaningful for storage analysis). Otherwise, fall back to `EventCount`. **Zero-value guard:** filter out items with value <= 0 before passing to the layout engine — zero-value items cause NaN in dimension calculations. When `SelectedTenant` is set, show domain → aggregate type hierarchy. When no tenant selected, show tenant → aggregate type.
  - [x] 3.4 Render SVG: `<svg>` with `viewBox="0 0 1600 900"` (16:9), `preserveAspectRatio="xMidYMid meet"`, `width="100%"`. Each rectangle is a `<g>` containing `<rect>` + `<text>` (label) + `<text>` (count). Color assignment: hash domain name to select from 10-color palette. Hover: `<title>` element for native browser tooltip with full details.
  - [x] 3.5 Add click handler on rectangles: fire `OnAggregateTypeSelected` with the clicked aggregate type. Highlight selected rectangle with a 2px white stroke.
  - [x] 3.6 Add "Other" bucketing: if > 500 distinct aggregate types, group the smallest into "Other (N types)" bucket.
  - [x] 3.7 Implement compact viewport fallback: when `!ViewportService.IsWideViewport`, render horizontal bar chart instead of treemap. Bars use same color palette. Show top 10 types.
  - [x] 3.8 Add toggle button ("Treemap" / "Bar Chart") above the visualization. Default mode based on viewport, but toggle overrides.
  - [x] 3.9 Add `role="img"` and `aria-label` to SVG. Add hidden `<table class="sr-only">` data table for screen readers.
  - [x] 3.10 **Checkpoint**: Treemap renders, click filtering works, responsive toggle works, accessible.

- [x] **Task 4: Breadcrumb, NavMenu, and Command Palette integration** (AC: 8, 14)
  - [x] 4.1 Update `Breadcrumb.razor` route label dictionary: add `"storage" -> "Storage"`.
  - [x] 4.2 Update `NavMenu.razor`: add `<FluentNavLink Href="/storage" Icon="@(new Icons.Regular.Size20.Database())">Storage</FluentNavLink>` between "Tenants" and "Settings". If `Icons.Regular.Size20.Database` doesn't exist, use `HardDrive` or `Storage`.
  - [x] 4.3 Update `CommandPaletteCatalog.cs`: add a "Storage" entry navigating to `/storage`. Follow existing entry pattern (e.g., "Health" → `/health`).
  - [x] 4.4 **Checkpoint**: Storage page accessible from sidebar, breadcrumb shows "Home / Storage", Ctrl+K "Storage" navigates correctly.

- [x] **Task 5: bUnit and unit tests** (AC: 1-16)
  - **Mock dependencies**: extend `AdminUITestContext`, mock `AdminStorageApiClient`. Note: `AdminUITestContext` does not register `AdminStorageApiClient` by default — each test class must register its own mock via `Services.AddScoped(_ => _mockStorageApi)`.
  - **Merge-blocking tests** (must pass):
  - [x] 5.1 Test `Storage` page renders 4 stat cards with correct values from `StorageOverview` (AC: 1)
  - [x] 5.2 Test `Storage` page shows `SkeletonCard` during loading state (AC: 1)
  - [x] 5.3 Test tenant breakdown grid renders all tenants with correct event counts (AC: 2)
  - [x] 5.4 Test hot streams grid renders streams with correct columns (AC: 3)
  - [x] 5.5 Test `IssueBanner` shown when API returns error (AC: 10)
  - [x] 5.6 Test "N/A" displayed when `TotalSizeBytes` is null (AC: 1, 10)
  - [x] 5.7 Test storage page has `<h1>Storage</h1>` heading (AC: 12)
  - [x] 5.8 Test `StorageTreemap` component renders SVG with `role="img"` and `aria-label` (AC: 4, 12)
  - [x] 5.9 Test `EmptyState` shown when `TenantBreakdown` is empty (AC: 15)
  - [x] 5.10 Test snapshot risk banner shows correct count of at-risk streams (AC: 16)
  - **Recommended tests**:
  - [x] 5.11 Test `TreemapLayoutEngine.ComputeLayout` returns correct number of rectangles (pure function test, no bUnit) (AC: 4)
  - [x] 5.12 Test `TreemapLayoutEngine.ComputeLayout` rectangle areas are proportional to input values (AC: 4)
  - [x] 5.12a Test `TreemapLayoutEngine` area conservation: sum of all output rectangle areas == container area (1600×900) (AC: 4, 13)
  - [x] 5.12b Test `TreemapLayoutEngine` no overlapping rectangles in output (AC: 4)
  - [x] 5.12c Test `TreemapLayoutEngine` handles zero-value and negative-value items gracefully — should be filtered out before layout, but if passed, no NaN/Infinity in output (AC: 13)
  - [x] 5.13 Test `StorageTreemap` fires `OnAggregateTypeSelected` on rectangle click (AC: 4)
  - [x] 5.14 Test tenant filter updates URL query parameter (AC: 6, 7)
  - [x] 5.15 Test hot streams grid shows warning icon for streams without snapshots (AC: 3)
  - [x] 5.16 Test growth rate badge colors: green < 1000, amber 1000-10000, red > 10000 (AC: 2)
  - [x] 5.17 Test `FormatBytes` helper: null → "N/A", 1024 → "1.0 KB", 1073741824 → "1.0 GB" (AC: 1, 2, 3)
  - [x] 5.18 Test `StorageTreemap` groups types into "Other" when > 500 types (AC: 13)
  - [x] 5.19 Test bar chart fallback renders when `ViewportService.IsWideViewport` is false (AC: 5)
  - [x] 5.20 Test hidden screen reader table rendered alongside treemap (AC: 12)
  - [x] 5.21 Test URL parameters read on page initialization (AC: 7)
  - [x] 5.22 Test treemap "Other" bucket label includes count of grouped types (AC: 13)

- [x] **Task 6: CSS and final polish** (AC: 5, 12)
  - [x] 6.1 Add CSS styles in `wwwroot/css/app.css` for storage page: `.storage-treemap-container` (responsive sizing, margin-bottom), `.storage-treemap rect:hover` (opacity change), `.storage-filter-bar` (flex row, gap, margin-bottom), `.storage-toggle-btn` (icon-only toggle button style). `.sr-only` class for hidden screen reader table (if not already defined).
  - [x] 6.2 Ensure treemap SVG does not cause horizontal scrolling at any viewport width.
  - [x] 6.3 **Checkpoint**: All ACs pass, zero warnings, page fully functional.

## Dev Notes

### Architecture Compliance

- **ADR-P4**: Admin.UI communicates exclusively via HTTP REST API to Admin.Server. The `AdminStorageApiClient` calls `api/v1/admin/storage/*` endpoints — no direct DAPR access.
- **SEC-5**: No event payload data on this page — only aggregate-level metadata (counts, sizes, types).
- **NFR41**: Initial render ≤ 2 seconds. Treemap computation runs server-side (Blazor Server) O(n log n) for squarified layout; SVG serialized to browser via SignalR. Memoize layout output to avoid recomputation on unrelated re-renders.
- **NFR44**: Size data may be null (state store backend dependent) — all size columns gracefully show "N/A".
- **NFR45**: Supports concurrent users — no shared mutable state. Page state is component-scoped.
- **FR76**: This story covers the "show growth trends" and "hot streams" portions of FR76. Compaction trigger (story 16-3) and snapshot creation (story 16-2) are separate.

### Scope — Read-Only Storage Analysis

This story is read-only. It does NOT include:
- Snapshot creation or policy management (story 16-2)
- Compaction triggering or scheduling (story 16-3)
- Backup operations (story 16-4)
- Tenant management (story 16-5)

All operator-level actions will be added in subsequent stories with proper role enforcement.

### Deferred Improvements (Out of Scope — Future Stories)

- **Capacity projection / "days until full"** — Requires server-side capacity threshold configuration per tenant (new DTO field + API change). High value for Maria's workflow (Journey 8 step 2). Recommend as first AC in story 16-2 or a dedicated mini-story.
- **Growth trend percentage** — Replace raw "Avg Growth/Day" with "Growth: +12%/week" for more intuitive DBA interpretation. Needs historical data points (at least 2 snapshots in time).
- **"Quick Actions" placeholder** — Show a disabled section below the hot streams grid: "Snapshot and compaction controls available in a future update" — signals to Maria that help is coming.

### Existing Code to Reuse (DO NOT Recreate)

| What | Where | How |
|------|-------|-----|
| `StorageOverview` DTO | `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/StorageOverview.cs` | USE — contains TotalEventCount, TotalSizeBytes, TenantBreakdown |
| `TenantStorageInfo` DTO | `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/TenantStorageInfo.cs` | USE — TenantId, EventCount, SizeBytes, GrowthRatePerDay |
| `StreamStorageInfo` DTO | `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/StreamStorageInfo.cs` | USE — TenantId, Domain, AggregateId, AggregateType, EventCount, SizeBytes, HasSnapshot, SnapshotAge |
| `SnapshotPolicy` DTO | `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/SnapshotPolicy.cs` | NOT USED in this story — belongs to story 16-2 |
| `AdminStorageController` | `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStorageController.cs` | SERVER-SIDE — endpoints already exist: overview, hot-streams, snapshot-policies |
| `AdminStreamApiClient` | `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs` | PATTERN MODEL — follow same constructor, HttpClient, error handling pattern |
| `HandleErrorStatus` method | `AdminStreamApiClient.cs` | COPY pattern — propagate UnauthorizedAccessException, ForbiddenAccessException, ServiceUnavailableException |
| `StatCard` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/StatCard.razor` | USE — Label, Value, Severity, Title |
| `SkeletonCard` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/SkeletonCard.razor` | USE — loading placeholder |
| `IssueBanner` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/IssueBanner.razor` | USE — error/warning state |
| `EmptyState` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/EmptyState.razor` | USE — no data fallback |
| `DashboardRefreshService` | `src/Hexalith.EventStore.Admin.UI/Services/DashboardRefreshService.cs` | DO NOT USE — this page uses manual refresh only. DashboardRefreshService fetches health/streams data, not storage data |
| `ViewportService` | `src/Hexalith.EventStore.Admin.UI/Services/ViewportService.cs` | USE — IsWideViewport for responsive treemap/bar toggle |
| `AdminUITestContext` | `tests/Hexalith.EventStore.Admin.UI.Tests/AdminUITestContext.cs` | USE — base test class with FluentUI, mock services, JSInterop |
| `Health.razor` | `src/Hexalith.EventStore.Admin.UI/Pages/Health.razor` | PATTERN MODEL — page structure, stat cards, loading/error/stale states |
| `Breadcrumb.razor` | `src/Hexalith.EventStore.Admin.UI/Layout/Breadcrumb.razor` | MODIFY — add "storage" → "Storage" to route label dictionary |
| `NavMenu.razor` | `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor` | MODIFY — add Storage nav link |
| `CommandPaletteCatalog.cs` | `src/Hexalith.EventStore.Admin.UI/Components/CommandPaletteCatalog.cs` | MODIFY — add "Storage" → `/storage` entry |
| `Program.cs` | `src/Hexalith.EventStore.Admin.UI/Program.cs` | MODIFY — register AdminStorageApiClient |

### AdminStorageApiClient — Exact API Endpoints

The server controller (`AdminStorageController`) exposes these REST endpoints:

| Method | URL | Params | Returns |
|--------|-----|--------|---------|
| GET | `api/v1/admin/storage/overview` | `?tenantId=` | `StorageOverview` |
| GET | `api/v1/admin/storage/hot-streams` | `?tenantId=&count=100` | `IReadOnlyList<StreamStorageInfo>` |
| GET | `api/v1/admin/storage/snapshot-policies` | `?tenantId=` | `IReadOnlyList<SnapshotPolicy>` |

All 3 endpoints require `ReadOnly` authorization policy + `AdminTenantAuthorizationFilter`.

### Treemap Algorithm — Squarified Layout

Use the squarified treemap algorithm (Bruls, Huizing, van Wijk 2000):

1. Sort items by value descending
2. Start with full rectangle (0, 0, width, height)
3. For each row of items: lay out in the direction that minimizes aspect ratio deviation from 1.0
4. Recursively fill remaining space with remaining items
5. Result: list of (x, y, w, h) for each item

The treemap viewBox is 1600x900 (16:9). Each rectangle gets:
- Fill color from domain-based palette
- Stroke: 1px white for visual separation
- Text: aggregate type name (skip if rect width < 60px or height < 20px)
- Count: formatted event count (skip if rect width < 40px)

Color palette (10 colors, CSS custom properties from FluentUI tokens):
```
["#0078D4", "#107C10", "#D83B01", "#8764B8", "#008272", "#E3008C", "#4F6BED", "#B4009E", "#C239B3", "#00B7C3"]
```
Assign by deterministic hash of domain name — do NOT use `string.GetHashCode()` (randomized per process in .NET). Use FNV-1a or a simple character sum:
```csharp
private static int DeterministicHash(string s)
{
    unchecked
    {
        int hash = 2166136261;
        foreach (char c in s) { hash = (hash ^ c) * 16777619; }
        return hash;
    }
}
// Usage: palette[(DeterministicHash(domain) & 0x7FFFFFFF) % 10]
// WARNING: Do NOT use Math.Abs() — Math.Abs(int.MinValue) throws OverflowException.
```
This ensures the same domain always gets the same color across page reloads and server restarts.

### FormatBytes Helper

```csharp
private static string FormatBytes(long? bytes)
{
    if (bytes is null) return "N/A";
    double value = bytes.Value;
    string[] units = ["B", "KB", "MB", "GB", "TB"];
    int i = 0;
    while (value >= 1024 && i < units.Length - 1) { value /= 1024; i++; }
    return $"{value:F1} {units[i]}";
}
```

### URL Parameter Parsing Pattern

Follow the existing `HttpUtility.ParseQueryString` approach used by Streams, Projections, TypeCatalog:

```csharp
private void ReadUrlParameters()
{
    Uri uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
    _tenantFilter = query["tenant"];
    _selectedAggregateType = query["type"];
    _sortColumn = query["sort"];
    _sortDirection = query["dir"];
}

private void UpdateUrl()
{
    List<string> queryParams = [];
    if (!string.IsNullOrEmpty(_tenantFilter))
        queryParams.Add($"tenant={Uri.EscapeDataString(_tenantFilter)}");
    if (!string.IsNullOrEmpty(_selectedAggregateType))
        queryParams.Add($"type={Uri.EscapeDataString(_selectedAggregateType)}");
    string url = queryParams.Count > 0 ? $"/storage?{string.Join('&', queryParams)}" : "/storage";
    NavigationManager.NavigateTo(url, forceLoad: false, replace: true);
}
```

### Page Lifecycle Pattern (Simplified — Manual Refresh Only)

Unlike Health.razor which uses `DashboardRefreshService` for auto-polling, this page uses manual refresh only. Storage metrics change slowly (hourly), so auto-polling wastes resources.

```csharp
@implements IAsyncDisposable

private StorageOverview? _overview;
private IReadOnlyList<StreamStorageInfo>? _hotStreams;
private StorageOverview? _cachedOverview;
private bool _isLoading = true;
private bool _isStale;
private bool _apiUnavailable;
private string? _tenantFilter;
private string? _selectedAggregateType;
private Timer? _debounceTimer;
private CancellationTokenSource? _loadCts;

protected override async Task OnInitializedAsync()
{
    ReadUrlParameters();
    await LoadDataAsync();
}

private async Task LoadDataAsync()
{
    // Cancel any in-flight request (prevents stale response overwriting newer filter)
    _loadCts?.Cancel();
    _loadCts = new CancellationTokenSource();
    CancellationToken ct = _loadCts.Token;

    _isLoading = _overview is null;
    try
    {
        _overview = await StorageApi.GetStorageOverviewAsync(_tenantFilter, ct);
        _hotStreams = await StorageApi.GetHotStreamsAsync(_tenantFilter, 100, ct);
        _cachedOverview = _overview;
        _apiUnavailable = false;
        _isStale = false;
    }
    catch (OperationCanceledException) { return; } // Superseded by newer request
    catch (ServiceUnavailableException)
    {
        _apiUnavailable = true;
        _isStale = _cachedOverview is not null;
    }
    finally
    {
        _isLoading = false;
        await InvokeAsync(StateHasChanged);
    }
}

// Debounce timer callback — MUST use InvokeAsync (Blazor Server threading)
private void OnDebounceElapsed(object? state)
{
    _ = InvokeAsync(async () =>
    {
        UpdateUrl();
        await LoadDataAsync();
    });
}

public async ValueTask DisposeAsync()
{
    _loadCts?.Cancel();
    _loadCts?.Dispose();
    if (_debounceTimer is not null)
    {
        await _debounceTimer.DisposeAsync();
    }
}
```

### bUnit Test Pattern

Follow `HealthPageTests` pattern:

```csharp
public class StoragePageTests : AdminUITestContext
{
    private readonly AdminStorageApiClient _mockStorageApi;

    public StoragePageTests()
    {
        _mockStorageApi = Substitute.For<AdminStorageApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStorageApiClient>.Instance);
        Services.AddScoped(_ => _mockStorageApi);
    }

    [Fact]
    public void StoragePage_RendersStatCards_WithCorrectValues()
    {
        StorageOverview overview = new(
            TotalEventCount: 150000,
            TotalSizeBytes: 1073741824,
            TenantBreakdown: [new TenantStorageInfo("tenant-1", 100000, 536870912, 500.0)]);

        _mockStorageApi.GetStorageOverviewAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(overview));
        _mockStorageApi.GetHotStreamsAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<StreamStorageInfo>>([]));

        IRenderedComponent<Storage> cut = Render<Storage>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("150,000"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("1.0 GB");
    }
}
```

### Previous Story Intelligence (15-8)

- Breadcrumb route label dictionary is a `static IReadOnlyDictionary<string, string>` in `Breadcrumb.razor`'s `@code` block — add `"storage"` entry there.
- All 184 Admin.UI tests pass after story 15-8 — no regressions allowed.
- `DashboardRefreshService.OnDataChanged` subscription pattern is proven and stable — but NOT used on this page (manual refresh only, storage data changes slowly).
- `ViewportService.IsWideViewport` + `OnViewportChanged` event pattern for responsive components is established.
- JSInterop in tests uses `Mode = Loose` — new JS methods work without explicit setup.
- FluentUI Blazor v4 icon components: use `@(new Icons.Regular.Size20.Database())` syntax (verify icon exists, fallback to `HardDrive` or `Storage`).

### Git Intelligence

Recent commits follow `feat:` conventional commit prefix for new features. Branch naming: `feat/story-16-1-storage-growth-analyzer`. PR workflow: feature branch → PR → merge to main.

### Project Structure Notes

Files to create:
- **CREATE**: `src/Hexalith.EventStore.Admin.UI/Services/AdminStorageApiClient.cs` — HTTP client for storage endpoints
- **CREATE**: `src/Hexalith.EventStore.Admin.UI/Pages/Storage.razor` — Storage page
- **CREATE**: `src/Hexalith.EventStore.Admin.UI/Components/StorageTreemap.razor` — Treemap visualization component
- **CREATE**: `src/Hexalith.EventStore.Admin.UI/Components/TreemapLayoutEngine.cs` — Pure function squarified layout algorithm (static class, independently testable)
- **CREATE**: `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/StoragePageTests.cs` — bUnit tests for Storage page
- **CREATE**: `tests/Hexalith.EventStore.Admin.UI.Tests/Components/StorageTreemapTests.cs` — bUnit tests for treemap component
- **CREATE**: `tests/Hexalith.EventStore.Admin.UI.Tests/Components/TreemapLayoutEngineTests.cs` — Unit tests for layout algorithm (pure function, no bUnit needed)

Files to modify:
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Program.cs` — register `AdminStorageApiClient`
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor` — add Storage nav link
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Layout/Breadcrumb.razor` — add "storage" route label
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Components/CommandPaletteCatalog.cs` — add "Storage" command palette entry
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css` — treemap and storage page styles

No server-side changes. No new DTOs. No Abstractions changes. Backend API already exists.

### References

- [Source: prd.md — Journey 8: Maria, the DBA — storage growth analyzer with treemap]
- [Source: prd.md — FR76: Admin tool can manage storage — show growth trends, hot streams]
- [Source: architecture.md — ADR-P4: Three-interface architecture, Admin.UI via HTTP to Admin.Server]
- [Source: architecture.md — NFR41: Admin Web UI render ≤ 2 seconds initial load]
- [Source: architecture.md — NFR44: All access through DAPR abstractions, backend-agnostic]
- [Source: ux-design-specification.md — UX-DR42: Deep linking for every view]
- [Source: ux-design-specification.md — UX-DR48: Virtualized rendering for large lists]
- [Source: Admin.Abstractions/Models/Storage/ — StorageOverview, TenantStorageInfo, StreamStorageInfo, SnapshotPolicy DTOs]
- [Source: Admin.Server/Controllers/AdminStorageController.cs — REST endpoints already implemented]
- [Source: Admin.UI/Services/AdminStreamApiClient.cs — HTTP client pattern to follow]
- [Source: Admin.UI/Pages/Health.razor — Page structure pattern to follow]
- [Source: sprint-change-proposal-2026-03-21-admin-tooling.md — Epic 16 story breakdown]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- bUnit SVG `<text>` tag conflicts with Razor's `<text>` directive — resolved by using `MarkupString` for SVG text elements
- Culture-dependent formatting in tests (F1 format produces "1,0" in French locale) — fixed by using `$"{1.0:F1}"` instead of hardcoded "1.0"
- FluentDataGrid `OnRowClick` attribute (not `RowClick`) — discovered from existing codebase patterns

### Completion Notes List

- **Task 1**: Created `AdminStorageApiClient` with `GetStorageOverviewAsync` and `GetHotStreamsAsync` methods following `AdminStreamApiClient` pattern. Registered as scoped in `Program.cs`. All public methods marked `virtual` for NSubstitute mocking.
- **Task 2**: Created `Storage.razor` page at `/storage` with 4 stat cards (Total Events, Total Storage/Total Streams fallback, Tenants, Avg Growth/Day), tenant breakdown grid with growth badges, hot streams grid with snapshot warnings, tenant filter with 300ms debounce, URL state persistence, manual refresh, error/stale/empty states, snapshot risk banner.
- **Task 3**: Created `TreemapLayoutEngine.cs` as pure static class implementing squarified treemap algorithm. Created `StorageTreemap.razor` component with SVG treemap, bar chart fallback for narrow viewports, toggle button, deterministic FNV-1a domain color hashing, "Other" bucket for >500 types, memoized layout, screen reader table.
- **Task 4**: Added `"storage" -> "Storage"` to breadcrumb labels, Storage nav link with Database icon in NavMenu, two command palette entries ("Storage" and "Storage Growth Analyzer").
- **Task 5**: Created 30 tests (10 merge-blocking, 20 recommended) across 3 test files. All 214 Admin.UI tests pass (184 existing + 30 new).
- **Task 6**: Added CSS for `.storage-treemap-container`, `.storage-filter-bar`, `.storage-toggle-btn`, treemap hover effects. SVG uses responsive `width=100%` with `viewBox` to prevent horizontal scroll.
- **Post-review hardening**: Added exact stream total support via `StorageOverview.TotalStreamCount`, updated server fallback and optional enrichment behavior, and updated UI semantics to show exact totals only for exact scope and best-effort totals for partial/prefix filtering.

### Change Log

- 2026-03-24: Story 16-1 implemented — Storage Growth Analyzer with Treemap visualization (all 6 tasks, 16 ACs)
- 2026-03-24: Post-review refinements completed — exact stream total semantics aligned across abstractions, server, UI, and tests.

### File List

**Created:**
- `src/Hexalith.EventStore.Admin.UI/Services/AdminStorageApiClient.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/Storage.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/StorageTreemap.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/TreemapLayoutEngine.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/StoragePageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/StorageTreemapTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/TreemapLayoutEngineTests.cs`

**Modified:**
- `src/Hexalith.EventStore.Admin.UI/Program.cs` (registered AdminStorageApiClient)
- `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor` (added Storage nav link)
- `src/Hexalith.EventStore.Admin.UI/Layout/Breadcrumb.razor` (added "storage" route label)
- `src/Hexalith.EventStore.Admin.UI/Components/CommandPaletteCatalog.cs` (added Storage entries)
- `src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css` (added storage page styles)
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/StorageOverview.cs` (added optional `TotalStreamCount` with validation)
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStorageQueryService.cs` (fallback/enrichment updates for stream totals)
- `src/Hexalith.EventStore.Admin.UI/Pages/Storage.razor` (exact vs best-effort stream total display logic and tenant filter refinement)
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Storage/StorageOverviewTests.cs` (stream count contract tests)
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStorageServiceTests.cs` (stream count enrichment and resilience tests)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/StoragePageTests.cs` (exact stream total display tests)
