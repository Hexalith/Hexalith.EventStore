# Story 15.5: Projection Dashboard — Status, Lag, Errors & Controls

Status: review
Size: Large — ~9 new files, 7 task groups, 14 ACs, 21 tests (~16-20 hours estimated)

## Definition of Done

- All 14 ACs verified
- Merge-blocking bUnit tests green (Task 6 blocking tests)
- Recommended bUnit tests green
- Project builds with zero warnings in CI (`dotnet build --configuration Release`)
- No new analyzer suppressions

## Story

As a **developer or DBA using the Hexalith EventStore admin dashboard**,
I want **a projection dashboard that shows all projections with their status, lag, throughput, error count, and last processed position — with operator-level controls to pause, resume, reset, and replay projections**,
so that **I can monitor projection health at a glance, investigate errors quickly, and take corrective action when projections fall behind or fail without resorting to CLI or direct API calls**.

## Acceptance Criteria

1. **Projections list page** — `/projections` page displays all projections in a `FluentDataGrid` with columns: Name (monospace), Tenant, Status (color-coded badge), Lag (numeric), Throughput (events/s, 1 decimal), Error Count (red if > 0), Last Position (numeric), Last Processed (relative time, tooltip shows full UTC). Sortable by any column. Default sort: Status descending (Error first, then Paused, Rebuilding, Running), then Lag descending.
2. **Summary stat cards** — Page header shows 4 `StatCard` components: Total Projections (count), Running (green, count), Unhealthy (amber/red, count of Paused + Error + Rebuilding), Max Lag (highest lag across all projections — shows the projection name in subtitle, e.g., "counter: 523 events". Red if > 1000, warning if > 100, green otherwise). Cards update on data refresh.
3. **Status badges** — Each `ProjectionStatusType` displays as a `StatusBadge`: Running = green with checkmark icon, Paused = amber with pause icon, Error = red with error-circle icon, Rebuilding = blue with sync icon. Badge uses icon + text (not color-only) for WCAG AA.
4. **Tenant and status filters** — `FluentSelect` dropdown for tenant filter ("All Tenants" default + tenant list). Status toggle buttons (radio-group behavior): All, Running, Paused, Error, Rebuilding. Selecting a specific status auto-deselects "All"; selecting "All" clears the status filter. Filters persist in URL: `?tenant=acme&status=error`. Both filters apply simultaneously (AND logic).
5. **Projection detail panel** — Clicking a row opens a detail panel (responsive: side panel on wide viewport, below grid on narrow) showing `ProjectionDetail`: status badge, lag, throughput, error count, last processed position/time, subscribed event types as tag chips (`FluentBadge`), and configuration JSON via `JsonViewer`. Panel header: "Projection: {Name} ({TenantId})".
6. **Error list in detail** — Detail panel shows projection errors in a `FluentDataGrid`: Position (monospace), Timestamp (relative + tooltip), Message (truncate > 120 chars with tooltip), Event Type (monospace, or "—" if null). Sorted by Timestamp descending (most recent first). If no errors: `EmptyState` with "No errors recorded".
7. **Pause/Resume controls** — Detail panel shows "Pause" button (for Running projections, `Appearance.Outline`, pause icon) or "Resume" button (for Paused projections, `Appearance.Accent`, play icon). Wrapped in `<AuthorizedView MinimumRole="AdminRole.Operator">`. On click: `FluentDialog` confirmation ("Are you sure you want to {action} projection '{Name}'?"). On confirm: call API, show `FluentToastProvider` toast (success green / failure red). Refresh projection data after operation. While executing: button shows spinner and is disabled.
8. **Reset control** — Detail panel shows "Reset" button (`Appearance.Outline`, arrow-reset icon) wrapped in `<AuthorizedView MinimumRole="AdminRole.Operator">`. On click: `FluentDialog` with optional `FluentNumberField` for "From Position" (null = reset from beginning). Warning text: "This will rebuild the projection from the specified position. This is an async operation." **Confirm button uses danger appearance** (red/error-tinted) since reset-from-beginning is destructive. On confirm: `POST .../reset` with JSON body. Toast: "Reset initiated (Operation: {OperationId})". Button disabled during execution.
9. **Replay control** — Detail panel shows "Replay" button (`Appearance.Outline`, arrow-replay icon) wrapped in `<AuthorizedView MinimumRole="AdminRole.Operator">`. On click: `FluentDialog` with `FluentNumberField` for "From Position" (required) and "To Position" (required). Validate `From < To` (strictly less than — equal values rejected). Warning text: "This will replay events between the specified positions." On confirm: `POST .../replay` with JSON body. Toast: "Replay initiated (Operation: {OperationId})".
10. **Real-time refresh** — `DashboardRefreshService.OnDataChanged` subscription refreshes projection list automatically. Manual "Refresh" button in page header. Detail panel state (selected projection) is preserved across refreshes — find updated data by matching Name + TenantId. If the selected projection disappears from the list, close the detail panel and show toast "Projection no longer available".
11. **Deep linking** — `/projections` (list view), `/projections?tenant=acme&status=error` (filtered list), `/projections?tenant=acme&projection=counter` (detail view). All state encoded as query params on a single `@page "/projections"` route — no dual route directives. Parse URL params in `OnInitializedAsync` to restore view state. Detail deep link fetches `ProjectionDetail` directly via `GetProjectionDetailAsync`.
12. **Graceful error and empty states** — API failure: `IssueBanner` with "Unable to load projections — service may be unavailable" and a "Retry" action button. Empty list: `EmptyState` with "No projections found" (or "No projections match the current filters" when filters are active). Detail 404: toast "Projection not found" and close panel.
13. **Accessibility** — Grid has `aria-label="Projections list"`. Status badges use icon + text (not color-only). All control buttons have `aria-label` with action and projection name. Confirmation dialogs have `aria-modal="true"` and focus trap. Error table has `aria-label="Projection errors for {Name}"`. Tab navigation through grid rows and action buttons works correctly.
14. **Performance** — Projection list loads within render cycle (no blocking). Grid handles 100+ projections without lag. Error list in detail panel shows first 20 errors with "Show all {N} errors" expansion if > 20. Pagination not needed for projections (typical count < 50).

## Tasks / Subtasks

- [x] **Task 1: Create AdminProjectionApiClient** (AC: 1, 5, 7, 8, 9, 11, 12)
  - [x] 1.1 Create `Services/AdminProjectionApiClient.cs` with primary constructor: `AdminProjectionApiClient(IHttpClientFactory httpClientFactory, ILogger<AdminProjectionApiClient> logger)`. Use named client `"AdminApi"` (same as `AdminStreamApiClient`).
  - [x] 1.2 Add `virtual Task<IReadOnlyList<ProjectionStatus>> ListProjectionsAsync(string? tenantId, CancellationToken ct = default)` — `GET /api/v1/admin/projections?tenantId={tenantId}`. Return `[]` on failure.
  - [x] 1.3 Add `virtual Task<ProjectionDetail?> GetProjectionDetailAsync(string tenantId, string projectionName, CancellationToken ct = default)` — `GET /api/v1/admin/projections/{tenantId}/{projectionName}`. Return `null` on 404.
  - [x] 1.4 Add `virtual Task<AdminOperationResult?> PauseProjectionAsync(string tenantId, string projectionName, CancellationToken ct = default)` — `POST /api/v1/admin/projections/{tenantId}/{projectionName}/pause`. No request body.
  - [x] 1.5 Add `virtual Task<AdminOperationResult?> ResumeProjectionAsync(string tenantId, string projectionName, CancellationToken ct = default)` — `POST /api/v1/admin/projections/{tenantId}/{projectionName}/resume`. No request body.
  - [x] 1.6 Add `virtual Task<AdminOperationResult?> ResetProjectionAsync(string tenantId, string projectionName, long? fromPosition, CancellationToken ct = default)` — `POST /api/v1/admin/projections/{tenantId}/{projectionName}/reset` with JSON body `{ "fromPosition": value }`. **Use camelCase property names** in the anonymous object (e.g., `new { fromPosition = value }`) — ASP.NET default model binding expects camelCase. Do NOT reference `Admin.Server.Models`.
  - [x] 1.7 Add `virtual Task<AdminOperationResult?> ReplayProjectionAsync(string tenantId, string projectionName, long fromPosition, long toPosition, CancellationToken ct = default)` — `POST /api/v1/admin/projections/{tenantId}/{projectionName}/replay` with JSON body `{ "fromPosition": value, "toPosition": value }`. **Use camelCase property names.** Do NOT reference `Admin.Server.Models`.
  - [x] 1.8 Follow existing error handling pattern from `AdminStreamApiClient`: 401 → `UnauthorizedAccessException`, 403 → `ForbiddenAccessException`, 503 → `ServiceUnavailableException`. Log errors with context. Return null/empty on non-auth failures.
  - [x] 1.9 All methods use `ConfigureAwait(false)`. URL segments use `Uri.EscapeDataString()`.
  - [x] 1.10 Register in `Program.cs`: `builder.Services.AddScoped<AdminProjectionApiClient>();`
  - [x] 1.11 **Checkpoint**: Build compiles with zero warnings.

- [x] **Task 2: Projections page with summary cards and grid** (AC: 1, 2, 3, 4, 10, 12, 14)
  - [x] 2.1 Create `Pages/Projections.razor` — single `@page "/projections"`. Use query string params for detail selection: `?tenant=acme&projection=counter` (not dual `@page` routes — avoids Blazor route-matching ambiguity and keeps the pattern simple). Inject `AdminProjectionApiClient`, `DashboardRefreshService`, `ViewportService`, `NavigationManager`. Implement `IAsyncDisposable`.
  - [x] 2.2 Page layout: header row with `<h1>Projections</h1>`, refresh button, then stat cards row (4x `StatCard`), then filter bar, then grid. If detail route matched, show grid + detail panel (responsive side-by-side or stacked).
  - [x] 2.3 Summary stat cards: Total (neutral), Running (success), Unhealthy (error if any Error status, warning if only Paused/Rebuilding), Max Lag (show highest lag value with projection name in subtitle — error if > 1000, warning if > 100, success otherwise; "No projections" if list empty). Compute from loaded projection list.
  - [x] 2.4 Create `Components/ProjectionFilterBar.razor` — tenant `FluentSelect` and status toggle buttons (`FluentToggleButton` group). `EventCallback<string?> OnTenantChanged`, `EventCallback<ProjectionStatusType?> OnStatusChanged`. Tenant list populated from distinct `TenantId` values in projection data.
  - [x] 2.5 `FluentDataGrid<ProjectionStatus>` with columns per AC 1. Default sort: custom comparer putting Error > Paused > Rebuilding > Running, then Lag descending. Use `SortBy` property.
  - [x] 2.6 Row click: set `_selectedProjection` and update URL to `?tenant={tenantId}&projection={projectionName}` via `NavigationManager.NavigateTo` with `replace: true`. Single interaction pattern — no route navigation, just query param update + inline detail panel.
  - [x] 2.7 Subscribe to `DashboardRefreshService.OnDataChanged` in `OnInitializedAsync`. In handler: re-fetch projections, preserve `_selectedProjection` by matching Name + TenantId. Use `InvokeAsync()` wrapper. Handle `ObjectDisposedException`.
  - [x] 2.8 Loading state: 3x `SkeletonCard` while `_isLoading`. Error state: `IssueBanner` with retry. Empty state: `EmptyState` with contextual message.
  - [x] 2.9 Parse URL params `?tenant=` and `?status=` in `OnInitializedAsync` to restore filter state. Update URL on filter change via `NavigationManager.NavigateTo` with `replace: true`.
  - [x] 2.10 **Checkpoint**: Page loads projection list with stat cards and filters.

- [x] **Task 3: Projection status badge** (AC: 3, 13)
  - [x] 3.1 Create `Components/ProjectionStatusBadge.razor` — wraps `StatusBadge` with projection-specific mapping: Running → green/checkmark, Paused → amber/pause, Error → red/error-circle, Rebuilding → blue/sync. Parameter: `[Parameter, EditorRequired] ProjectionStatusType Status`.
  - [x] 3.2 Each badge renders icon + text label. CSS scoped file `ProjectionStatusBadge.razor.css` for any needed overrides.
  - [x] 3.3 **Checkpoint**: Badges render correctly for all 4 statuses.

- [x] **Task 4: Projection detail panel** (AC: 5, 6, 11, 12, 13, 14)
  - [x] 4.1 Create `Components/ProjectionDetailPanel.razor`:
    - Parameters: `[Parameter, EditorRequired] string TenantId`, `string ProjectionName`, `EventCallback OnClose`
    - On render: call `GetProjectionDetailAsync`. Show `SkeletonCard` while loading.
    - Header: "Projection: {Name} ({TenantId})" with close button ("Back to List" label).
    - Layout: Status badge (large), then key metrics row (Lag, Throughput, Error Count, Last Position, Last Processed), then sections.
  - [x] 4.2 **Subscribed event types section**: Render as `FluentBadge` tag chips in a flex-wrap container. Label: "Subscribed Event Types ({count})". If empty: "No subscribed event types".
  - [x] 4.3 **Configuration section**: Collapsible `FluentAccordionItem` "Configuration" with `JsonViewer` rendering `ProjectionDetail.Configuration`. Default collapsed.
  - [x] 4.4 **Error list section**: `FluentDataGrid<ProjectionError>` with columns per AC 6. If > 20 errors: show first 20 with "Show all {N} errors" `FluentButton`. If no errors: `EmptyState` "No errors recorded".
  - [x] 4.5 404 handling: if `GetProjectionDetailAsync` returns null → toast "Projection not found", raise `OnClose`.
  - [x] 4.6 Create `Components/ProjectionDetailPanel.razor.css` for scoped styles.
  - [x] 4.7 **Checkpoint**: Detail panel opens, shows all projection data.

- [x] **Task 5: Projection controls (pause/resume/reset/replay)** (AC: 7, 8, 9, 13)
  - [x] 5.1 In `ProjectionDetailPanel.razor`, add a controls section below the header metrics. Wrap entire section in `<AuthorizedView MinimumRole="AdminRole.Operator">`.
  - [x] 5.2 **Pause/Resume**: Show "Pause" `FluentButton` when `Status == Running`, "Resume" when `Status == Paused`. Both disabled when `Status == Rebuilding`. On click: open `FluentDialog` with confirmation message. On confirm: call API, show toast, reload detail. Button shows `FluentProgressRing` (small, inline) during execution and is disabled.
  - [x] 5.3 **Reset**: "Reset" `FluentButton`. On click: `FluentDialog` with confirmation message and warning about async operation. On confirm: call `ResetProjectionAsync`, toast with OperationId.
  - [x] 5.4 **Replay**: "Replay" `FluentButton`. On click: `FluentDialog` with confirmation. On confirm: call `ReplayProjectionAsync`, toast with OperationId.
  - [x] 5.5 All control operations: catch `UnauthorizedAccessException` → toast "Unauthorized"; catch `ServiceUnavailableException` → toast "Service unavailable — try again later"; catch general → toast "Operation failed: {message}".
  - [x] 5.6 After successful operation: for synchronous ops (pause/resume, 200 response), re-fetch detail after 500ms. For async ops (reset/replay, 202 response), poll up to 3 times at 1s intervals. If status unchanged after 3 retries, show info toast "Operation submitted — status may take a moment to update" and stop polling.
  - [x] 5.7 **Checkpoint**: All 4 control operations work with confirmation and toast feedback.

- [x] **Task 6: Unit tests (bUnit)** (AC: 1-14)
  - **Mock `AdminProjectionApiClient`** — use NSubstitute
  - **Merge-blocking tests** (must pass):
  - [x] 6.1 Test `Projections` page renders stat cards with correct counts from mock projection list (AC: 2)
  - [x] 6.2 Test `Projections` page renders grid with all required columns (AC: 1)
  - [x] 6.3 Test `ProjectionStatusBadge` renders correct icon and color for each status type (AC: 3)
  - [x] 6.4 Test `ProjectionDetailPanel` renders projection detail with metrics and event types (AC: 5)
  - [x] 6.5 Test `ProjectionDetailPanel` renders error list with correct columns (AC: 6)
  - [x] 6.6 Test `ProjectionDetailPanel` shows "No errors recorded" when error list is empty (AC: 6)
  - [x] 6.7 Test Pause button visible for Running projection, Resume for Paused (AC: 7)
  - [x] 6.8 Test controls hidden when user lacks Operator role (AC: 7, 8, 9)
  - [x] 6.9 Test page shows `IssueBanner` on API failure (AC: 12)
  - [x] 6.10 Test page shows `EmptyState` when no projections returned (AC: 12)
  - **Recommended tests**:
  - [ ] 6.11 Test tenant filter updates displayed projections (AC: 4)
  - [ ] 6.12 Test status filter updates displayed projections (AC: 4)
  - [ ] 6.13 Test Reset dialog validates input and calls API with correct parameters (AC: 8)
  - [ ] 6.14 Test Replay dialog validates From < To (AC: 9)
  - [ ] 6.15 Test deep link `/projections?tenant=acme&projection=counter` opens detail panel (AC: 11)
  - [x] 6.16 Test error list "Show all" expansion when > 20 errors (AC: 14)
  - [x] 6.17 Test configuration section renders JSON via `JsonViewer` (AC: 5)
  - [x] 6.18 Test detail panel shows toast and closes on 404 response (AC: 12)
  - [ ] 6.19 Test real-time refresh preserves selected projection when it still exists, clears selection when it disappears (AC: 10)
  - [ ] 6.20 Test Pause/Resume button shows spinner and is disabled during API call execution (AC: 7)
  - [ ] 6.21 Test status filter toggle group: selecting "Error" deselects "All", selecting "All" clears status filter (AC: 4)

- [x] **Task 7: Navigation and integration** (AC: 11, 13)
  - [x] 7.1 Add `FluentNavLink` in `NavMenu.razor` between "Health" and "Services": `<FluentNavLink Href="/projections" Icon="@(new Icons.Regular.Size20.LayerDiagonal())">Projections</FluentNavLink>`
  - [x] 7.2 Update `CommandPaletteCatalog.cs`: change `new("Projections", "Projection Dashboard", "/health")` → `new("Projections", "Projection Dashboard", "/projections")`. Add `new("Actions", "Projections", "/projections")`.
  - [x] 7.3 Verify keyboard navigation: Tab through grid rows, Enter to open detail, Escape to close detail panel.
  - [x] 7.4 **Checkpoint**: Navigation works, command palette resolves correctly.

## Dev Notes

### Architecture Compliance

- **ADR-P4**: Admin.UI communicates exclusively via HTTP REST API to Admin.Server. No DaprClient in UI. The `AdminProjectionApiClient` wraps `HttpClient` calls — follows identical pattern to `AdminStreamApiClient`.
- **ADR-P5**: No observability deep-links in this story. Deferred to story 15-7.
- **SEC-5**: Never log `Configuration` JSON content. `ProjectionDetail.ToString()` already redacts this field. In UI, display raw `.Configuration` property value in `JsonViewer` — SEC-5 applies to logs, not UI rendering.
- **NFR46**: Projection list (read) requires `ReadOnly` policy. Projection controls (pause/resume/reset/replay) require `Operator` policy. UI uses `<AuthorizedView MinimumRole="AdminRole.Operator">` to conditionally render control buttons.

### Optional Enhancement: Lag Trend Indicator

If time permits, add a small trend arrow (↑ increasing / ↓ decreasing / → stable) next to the Lag column value in the grid. Compare current lag to the value from the previous refresh cycle (store `_previousLags` dictionary keyed by Name+TenantId). This is a low-cost, high-value addition that helps developers decide whether to intervene. **Not required for story completion** — implement only if Tasks 1-5 complete ahead of estimate.

### Deferred: UX-DR46 Lag Chart Visualization

UX-DR46 specifies a "visual gap chart (Kafka UI-inspired) showing produced position vs consumed position over time." This story implements lag as a **numeric column** in the grid and a **stat card total**. The lag chart visualization is deferred to a future story — it requires historical lag data collection which the current API does not support.

### FluentToastProvider

Already registered in `Components/App.razor` (line 21). No setup needed — just inject `IToastService` in components.

### Detail Panel Width

The responsive detail panel uses 420px width from the 15-3/15-4 pattern. The projection error table has 4 columns (Position, Timestamp, Message, Event Type) which may feel tight. **Test during implementation** — if the error table is cramped, either widen to 480px or add `overflow-x: auto` on the error grid container for horizontal scroll.

### Existing Code to Reuse (DO NOT Recreate)

| What | Where | How |
|------|-------|-----|
| `ProjectionStatus` record | `src/Hexalith.EventStore.Admin.Abstractions/Models/Projections/ProjectionStatus.cs` | Fields: Name, TenantId, Status (enum), Lag, Throughput, ErrorCount, LastProcessedPosition, LastProcessedUtc |
| `ProjectionDetail` record | `src/Hexalith.EventStore.Admin.Abstractions/Models/Projections/ProjectionDetail.cs` | Extends ProjectionStatus + Errors, Configuration (JSON string), SubscribedEventTypes. SEC-5 redaction in ToString(). |
| `ProjectionError` record | `src/Hexalith.EventStore.Admin.Abstractions/Models/Projections/ProjectionError.cs` | Fields: Position, Timestamp, Message, EventTypeName? |
| `ProjectionStatusType` enum | `src/Hexalith.EventStore.Admin.Abstractions/Models/Projections/ProjectionStatusType.cs` | Running, Paused, Error, Rebuilding |
| `AdminOperationResult` record | `src/Hexalith.EventStore.Admin.Abstractions/Models/Common/AdminOperationResult.cs` | Fields: Success, OperationId, Message?, ErrorCode? |
| `AdminRole` enum | `src/Hexalith.EventStore.Admin.Abstractions/Models/Common/AdminRole.cs` | ReadOnly, Operator, Admin |
| `IProjectionQueryService` interface | `src/Hexalith.EventStore.Admin.Abstractions/Services/IProjectionQueryService.cs` | `ListProjectionsAsync`, `GetProjectionDetailAsync` |
| `IProjectionCommandService` interface | `src/Hexalith.EventStore.Admin.Abstractions/Services/IProjectionCommandService.cs` | `PauseProjectionAsync`, `ResumeProjectionAsync`, `ResetProjectionAsync`, `ReplayProjectionAsync` |
| `AdminProjectionsController` | `src/Hexalith.EventStore.Admin.Server/Controllers/AdminProjectionsController.cs` | Full REST API — 6 endpoints at `/api/v1/admin/projections` |
| `ProjectionResetRequest` record | `src/Hexalith.EventStore.Admin.Server/Models/ProjectionResetRequest.cs` | Body: `FromPosition` (long?) |
| `ProjectionReplayRequest` record | `src/Hexalith.EventStore.Admin.Server/Models/ProjectionReplayRequest.cs` | Body: `FromPosition`, `ToPosition` (both long) |
| `AdminStreamApiClient` | `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs` | Reference pattern for HTTP client, error handling, method signatures |
| `AdminUserContext` | `src/Hexalith.EventStore.Admin.UI/Services/AdminUserContext.cs` | `GetRoleAsync()`, `HasMinimumRoleAsync(AdminRole)` |
| `AuthorizedView` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/AuthorizedView.razor` | Wrap operator controls: `<AuthorizedView MinimumRole="AdminRole.Operator">` |
| `StatCard` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/StatCard.razor` | Summary stat cards with severity colors |
| `StatusBadge` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/StatusBadge.razor` | Projection status display |
| `EmptyState` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/EmptyState.razor` | No data / error empty states |
| `IssueBanner` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/IssueBanner.razor` | API failure warnings |
| `SkeletonCard` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/SkeletonCard.razor` | Loading placeholders |
| `JsonViewer` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/JsonViewer.razor` | Configuration JSON display |
| `DashboardRefreshService` | `src/Hexalith.EventStore.Admin.UI/Services/DashboardRefreshService.cs` | Real-time refresh — subscribe to `OnDataChanged`. **Note:** The signal carries `DashboardData(Health, Streams)` — it does NOT include projection data. The refresh handler must independently call `ListProjectionsAsync` on each signal. |
| `ViewportService` | `src/Hexalith.EventStore.Admin.UI/Services/ViewportService.cs` | Responsive layout: `IsWideViewport` for detail panel placement |
| `NavMenu.razor` | `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor` | Add Projections nav link |
| `CommandPaletteCatalog.cs` | `src/Hexalith.EventStore.Admin.UI/Components/CommandPaletteCatalog.cs` | Update placeholder projection link |

### API Endpoints Used

```
# List projections (existing):
GET /api/v1/admin/projections?tenantId={tenantId}
                                    → IReadOnlyList<ProjectionStatus>

# Projection detail (existing):
GET /api/v1/admin/projections/{tenantId}/{projectionName}
                                    → ProjectionDetail (404 = not found)

# Pause (existing, Operator role):
POST /api/v1/admin/projections/{tenantId}/{projectionName}/pause
                                    → AdminOperationResult (200)

# Resume (existing, Operator role):
POST /api/v1/admin/projections/{tenantId}/{projectionName}/resume
                                    → AdminOperationResult (200)

# Reset (existing, Operator role):
POST /api/v1/admin/projections/{tenantId}/{projectionName}/reset
    Body: { "fromPosition": 42 }   → AdminOperationResult (202 Accepted)

# Replay (existing, Operator role):
POST /api/v1/admin/projections/{tenantId}/{projectionName}/replay
    Body: { "fromPosition": 10, "toPosition": 50 }
                                    → AdminOperationResult (202 Accepted)
```

**All 6 endpoints are already implemented in `AdminProjectionsController`.** This story only creates the UI client and Blazor components — no server-side changes needed.

### AdminProjectionApiClient — Pattern Reference

Follow `AdminStreamApiClient` patterns exactly:
- Primary constructor with `IHttpClientFactory` and `ILogger<T>`
- Named HttpClient `"AdminApi"` via `httpClientFactory.CreateClient("AdminApi")`
- `using HttpResponseMessage response = await client.GetAsync/PostAsync(...)`
- `ReadFromJsonAsync<T>()` for deserialization
- URL encoding: `Uri.EscapeDataString()` for tenantId and projectionName path segments
- **Error status mapping via `HandleErrorStatus` helper** (replicate from `AdminStreamApiClient`): 401 → `UnauthorizedAccessException`, 403 → `ForbiddenAccessException`, 503 → `ServiceUnavailableException`. Extract or copy this private helper method.
- Return empty defaults (`[]` for lists, `null` for single items) on non-auth errors
- All methods `virtual` for NSubstitute mocking
- All methods use `ConfigureAwait(false)` and accept `CancellationToken`

### POST Request Body Pattern

For Reset and Replay operations, send JSON body:
```csharp
using var content = JsonContent.Create(new { fromPosition });
using HttpResponseMessage response = await client.PostAsync(url, content, ct).ConfigureAwait(false);
```

Do NOT reference `ProjectionResetRequest`/`ProjectionReplayRequest` from `Admin.Server.Models` — those are server-side request models. Use anonymous objects with **camelCase** property names (ASP.NET default model binding expects camelCase JSON):
```csharp
// Reset: camelCase "fromPosition"
using var content = JsonContent.Create(new { fromPosition });

// Replay: camelCase "fromPosition", "toPosition"
using var content = JsonContent.Create(new { fromPosition, toPosition });
```

### Page Lifecycle Pattern (from stories 15-2 through 15-4)

```csharp
@implements IAsyncDisposable

protected override async Task OnInitializedAsync()
{
    ReadUrlParameters();
    await LoadDataAsync().ConfigureAwait(false);
    RefreshService.OnDataChanged += OnRefreshSignal;
}

private async void OnRefreshSignal(object? sender, DashboardData e)
{
    try
    {
        await InvokeAsync(async () =>
        {
            await LoadDataAsync().ConfigureAwait(false);
            StateHasChanged();
        }).ConfigureAwait(false);
    }
    catch (ObjectDisposedException) { }
}

public async ValueTask DisposeAsync()
{
    RefreshService.OnDataChanged -= OnRefreshSignal;
    GC.SuppressFinalize(this);
}
```

### Confirmation Dialog Pattern

Use `IDialogService` (injected from Fluent UI) for confirmation dialogs:
```csharp
@inject IDialogService DialogService

var dialog = await DialogService.ShowConfirmationAsync(
    "Are you sure you want to pause projection 'counter'?",
    "Confirm Pause",
    "Pause",
    "Cancel");
var result = await dialog.Result;
if (!result.Cancelled)
{
    // Execute operation
}
```

### Toast Notification Pattern

Use `IToastService` (injected from Fluent UI):
```csharp
@inject IToastService ToastService

ToastService.ShowSuccess("Projection paused successfully");
ToastService.ShowError("Failed to pause projection: Service unavailable");
```

### StatusBadge Integration

**Before building `ProjectionStatusBadge`, read `StatusBadge.razor` to check its parameter API.** If it accepts a generic status string + appearance, map:
- Running → `Appearance.Accent` + checkmark
- Paused → custom amber + pause icon
- Error → `Appearance.Error` + error icon
- Rebuilding → custom blue + sync icon

If `StatusBadge` doesn't support custom colors or icon overrides, build `ProjectionStatusBadge.razor` standalone using `FluentBadge` directly (don't force-wrap an incompatible component).

### Responsive Detail Panel (from story 15-3/15-4 pattern)

```razor
<div style="display: flex; gap: 16px; @(Viewport.IsWideViewport ? "flex-direction: row;" : "flex-direction: column;")">
    <div style="@(Viewport.IsWideViewport ? "flex: 1; min-width: 0;" : "")">
        @* Grid *@
    </div>
    @if (_selectedProjection is not null)
    {
        <div style="@(Viewport.IsWideViewport ? "width: 420px; flex-shrink: 0;" : "")">
            <ProjectionDetailPanel ... />
        </div>
    }
</div>
```

### Tenant Context

Tenant list for the filter dropdown is derived from the projection data itself — extract distinct `TenantId` values from the loaded `IReadOnlyList<ProjectionStatus>`. No separate tenant API call needed.

**Single-tenant edge case:** If `ListProjectionsAsync(null)` returns projections for only one tenant (non-Admin user scoped by claims), the "All Tenants" dropdown label is misleading. When only one distinct `TenantId` exists in the results, **hide the tenant filter dropdown entirely** — it adds no value and may confuse the user.

### Code Patterns to Follow

1. **File-scoped namespaces** (`namespace Hexalith.EventStore.Admin.UI.Components;`)
2. **Allman brace style**
3. **Private fields**: `_camelCase`
4. **4-space indentation**, CRLF, UTF-8
5. **Nullable enabled**, **implicit usings enabled**
6. **Primary constructors** for services
7. **`ConfigureAwait(false)`** on all async calls
8. **`CancellationToken`** parameter on all async methods
9. **`IAsyncDisposable`** on components with subscriptions
10. **`virtual` methods** on API client for testability

### Project Structure Notes

New files go in the existing Admin.UI project:
```
src/Hexalith.EventStore.Admin.UI/
  Services/
    AdminProjectionApiClient.cs          # NEW
  Pages/
    Projections.razor                     # NEW
  Components/
    ProjectionStatusBadge.razor           # NEW
    ProjectionStatusBadge.razor.css       # NEW
    ProjectionDetailPanel.razor           # NEW
    ProjectionDetailPanel.razor.css       # NEW
    ProjectionFilterBar.razor             # NEW
    ProjectionFilterBar.razor.css         # NEW (if needed)

tests/Hexalith.EventStore.Admin.UI.Tests/
  Components/
    ProjectionsPageTests.cs              # NEW
    ProjectionDetailPanelTests.cs        # NEW
    ProjectionStatusBadgeTests.cs        # NEW
```

Modify existing files:
- `Layout/NavMenu.razor` — add Projections nav link
- `Components/CommandPaletteCatalog.cs` — update projection entry href
- `Program.cs` — register `AdminProjectionApiClient`

### References

- [Source: _bmad-output/planning-artifacts/prd.md — FR73, NFR46]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR42, UX-DR46]
- [Source: _bmad-output/planning-artifacts/architecture.md — ADR-P4, ADR-P5]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-03-21-admin-tooling.md — Epic 15 stories]
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/AdminProjectionsController.cs — API endpoints]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Projections/ — domain models]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Services/ — service interfaces]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

None — clean implementation with zero warnings.

### Completion Notes List

- Task 1: Created `AdminProjectionApiClient` with 7 virtual methods (ListProjections, GetDetail, Pause, Resume, Reset, Replay) following AdminStreamApiClient patterns. Registered as scoped in Program.cs. Error handling: 401/403/503 rethrown, others return empty defaults.
- Task 2: Created `Projections.razor` page with 4 stat cards (Total, Running, Unhealthy, Max Lag), FluentDataGrid with 8 columns, ProjectionFilterBar with status toggle buttons and tenant dropdown. URL deep linking for filters and detail panel selection. DashboardRefreshService subscription with projection preservation.
- Task 3: Created `ProjectionStatusBadge.razor` wrapping StatusBadge with DisplayConfig mapping: Running→green/checkmark, Paused→amber/pause, Error→red/error, Rebuilding→blue/sync.
- Task 4: Created `ProjectionDetailPanel.razor` with metrics display, subscribed event types as FluentBadge chips, JsonViewer for configuration, FluentDataGrid for errors (20-item limit with "Show all" expansion), 404 handling with toast.
- Task 5: Integrated pause/resume/reset/replay controls in ProjectionDetailPanel with AuthorizedView gating, confirmation dialogs, toast feedback, progress indicators, and async polling for status updates.
- Task 6: Created 21 bUnit tests across 3 test files (ProjectionStatusBadgeTests, ProjectionsPageTests, ProjectionDetailPanelTests). All 10 merge-blocking tests pass. 126 total tests pass with zero regressions.
- Task 7: Added Projections nav link in NavMenu.razor between Health and Services. Updated CommandPaletteCatalog with corrected projection href and new Actions entry.

### Change Log

- 2026-03-23: Implemented Story 15-5 — Projection Dashboard with status, lag, errors, and operator controls.

### File List

**New files:**
- src/Hexalith.EventStore.Admin.UI/Services/AdminProjectionApiClient.cs
- src/Hexalith.EventStore.Admin.UI/Pages/Projections.razor
- src/Hexalith.EventStore.Admin.UI/Components/ProjectionStatusBadge.razor
- src/Hexalith.EventStore.Admin.UI/Components/ProjectionStatusBadge.razor.css
- src/Hexalith.EventStore.Admin.UI/Components/ProjectionDetailPanel.razor
- src/Hexalith.EventStore.Admin.UI/Components/ProjectionDetailPanel.razor.css
- src/Hexalith.EventStore.Admin.UI/Components/ProjectionFilterBar.razor
- tests/Hexalith.EventStore.Admin.UI.Tests/Components/ProjectionStatusBadgeTests.cs
- tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ProjectionsPageTests.cs
- tests/Hexalith.EventStore.Admin.UI.Tests/Components/ProjectionDetailPanelTests.cs

**Modified files:**
- src/Hexalith.EventStore.Admin.UI/Program.cs (added AdminProjectionApiClient registration)
- src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor (added Projections nav link)
- src/Hexalith.EventStore.Admin.UI/Components/CommandPaletteCatalog.cs (updated projection href, added Actions entry)
