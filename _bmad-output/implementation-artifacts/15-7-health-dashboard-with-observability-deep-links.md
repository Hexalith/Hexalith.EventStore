# Story 15.7: Health Dashboard with Observability Deep Links

Status: ready-for-dev
Size: Medium — ~5 new/modified files, 5 task groups, 11 ACs, 15 tests (~8-12 hours estimated)

## Definition of Done

- All 11 ACs verified
- Merge-blocking bUnit tests green (Task 4 blocking tests)
- Recommended bUnit tests green
- Project builds with zero warnings in CI (`dotnet build --configuration Release`)
- No new analyzer suppressions

## Story

As a **developer or DBA using the Hexalith EventStore admin dashboard**,
I want **a health dashboard page that shows system-wide operational health — overall status, event throughput, error rate, DAPR component status, and configurable deep links to external observability tools (Zipkin/Jaeger, Prometheus/Grafana, Aspire Dashboard)**,
so that **I can assess system health at a glance in under 2 minutes and seamlessly navigate to detailed observability data when investigation is needed, without resorting to separate monitoring tools for initial triage**.

## Acceptance Criteria

1. **Health dashboard page** — `/health` page replaces the current placeholder. Page title: "Health - Hexalith EventStore". Header: `<h1>Health</h1>` with a refresh button (triggers `DashboardRefreshService.TriggerImmediateRefreshAsync()`). Overall status rendered as a prominent `FluentBadge` with semantic color: Green = Healthy, Yellow/Amber = Degraded, Red = Unhealthy (UX-DR40). Badge includes icon + text label (not color-only, per AC 10).
2. **Summary stat cards** — 4 `StatCard` components in a responsive grid row: (a) Total Events (`_healthReport.TotalEventCount`, neutral severity), (b) Events/sec (`_healthReport.EventsPerSecond`, formatted to 1 decimal — severity: "success" if > 0, "neutral" if 0), (c) Error Rate (`_healthReport.ErrorPercentage`, formatted as percentage to 1 decimal — severity: "success" if < 1%, "warning" if 1-5%, "error" if > 5%), (d) DAPR Components (display "{healthy}/{total}" — subtitle: "{N} component types" for quick context — severity: "success" if all healthy, "warning" if any degraded, "error" if any unhealthy). Cards update on every refresh. Show `SkeletonCard` while `_isLoading`.
3. **DAPR component grid** — `FluentDataGrid<DaprComponentHealth>` with columns: Component Name (monospace, sortable), Component Type (sortable), Status (`TemplateColumn` with `StatusBadge` — health severity mapping: Healthy→success, Degraded→warning, Unhealthy→error — **must include explicit `SortBy` parameter** sorting by the `HealthStatus` enum integer value since `TemplateColumn` has no automatic sort), Last Check (relative time using `TimeAgo` helper, `TemplateColumn` with `SortBy` on `LastCheckUtc`). Default sort: Status descending (unhealthy first), then ComponentName ascending. Grid has `aria-label="DAPR component health status"`.
4. **Observability deep-link buttons** — Section below DAPR grid: "Observability Tools" heading with up to 3 link buttons: (a) "View Traces" → `ObservabilityLinks.TraceUrl` (icon: `Icons.Regular.Size20.BranchFork`), (b) "View Metrics" → `ObservabilityLinks.MetricsUrl` (icon: `Icons.Regular.Size20.ChartMultiple`), (c) "View Logs" → `ObservabilityLinks.LogsUrl` (icon: `Icons.Regular.Size20.DocumentText`). **Must use anchor elements** (`<a href="@url" target="_blank" rel="noopener noreferrer">`) styled as outline buttons — do NOT use `FluentButton` with JS interop `window.open()`. Anchor elements provide proper browser behavior (right-click→open-in-new-tab, URL hover preview, accessibility). Use `FluentButton As="a"` if FluentUI supports it, or a plain `<a>` with button CSS class. If a URL is null/empty, that button is hidden (UX-DR47, ADR-P5 — graceful degradation). If ALL URLs are null, hide the entire section. Each link has `aria-label` (e.g., "Open traces in external observability tool").
5. **Auto-refresh via DashboardRefreshService** — Subscribe to `DashboardRefreshService.OnDataChanged` in `OnInitializedAsync`. Handler receives `DashboardData.Health` (`SystemHealthReport?`). On successful refresh: update `_healthReport`, clear stale flag, update `_lastRefreshUtc`. On null health: set `_isStale = true`, keep `_cachedHealthReport` for display. Use `InvokeAsync()` wrapper. Handle `ObjectDisposedException`. Implement `IAsyncDisposable` to unsubscribe.
6. **Initial load performance** — Load health data via `AdminStreamApiClient.GetSystemHealthAsync()` in `OnInitializedAsync`. Must render within 2 seconds (NFR41). Single API call — `SystemHealthReport` contains all needed data (DaprComponents, ObservabilityLinks).
7. **Deep linking** — `/health` restores default view. No additional URL params needed for this page (single-view page, unlike multi-tab pages). Preserve standard URL behavior — NavigationManager not modified beyond initial route.
8. **Error state** — API failure: `IssueBanner` with title "Unable to load health status" and description "The admin backend service may be unavailable. Check DAPR sidecar connectivity." and a "Retry" action button (calls `LoadDataAsync`). Show banner when `_apiUnavailable && _cachedHealthReport is null`.
9. **Stale data indicator** — When API becomes unavailable after a successful load: show stale banner "Health data may be stale — last updated {_lastRefreshUtc relative time}" with amber severity. Display `_cachedHealthReport` data (last-known-good). Show when `_isStale && _cachedHealthReport is not null`.
10. **Accessibility** — Overall status badge uses icon + text (not color-only). Grid has `aria-label`. Deep-link buttons have `aria-label`. Stat cards have `Title` parameter for tooltip. Status colors follow UX-DR40: Green = Healthy, Yellow = Degraded, Red = Unhealthy, Gray = Unknown.
11. **Responsive layout** — Stat cards use `FluentGrid` with 4 columns on wide viewport (≥1280px), 2 columns on narrow. DAPR grid maintains full width. Observability buttons stack vertically on narrow viewport. Use `ViewportService.IsWideViewport` for layout decisions.

## Tasks / Subtasks

- [ ] **Task 1: Implement Health.razor page** (AC: 1, 2, 5, 6, 7, 8, 9, 11)
  - [ ] 1.1 Replace placeholder content in `src/Hexalith.EventStore.Admin.UI/Pages/Health.razor`. Keep `@page "/health"`. Add `@implements IAsyncDisposable`. Inject `AdminStreamApiClient`, `DashboardRefreshService`, `ViewportService`, `NavigationManager`.
  - [ ] 1.2 Page layout: header row with `<h1>Health</h1>` + overall status `FluentBadge` + refresh `FluentButton`. Then stat cards row (4x `StatCard`). Then DAPR component grid section. Then observability deep-links section.
  - [ ] 1.3 Summary stat cards: Total Events (neutral), Events/sec (dynamic severity), Error Rate (dynamic severity), DAPR Components ("{healthy}/{total}", dynamic severity). Compute from `_healthReport`. Use `SkeletonCard` while `_isLoading`.
  - [ ] 1.4 Overall status badge: map `HealthStatus` → `FluentBadge` with filled style. Healthy = green fill + checkmark icon. Degraded = amber fill + warning icon. Unhealthy = red fill + error icon. Include text label. **Verify at build time:** check whether FluentUI Blazor v4.13.2 `FluentBadge` supports `Appearance="Appearance.Filled"` — if not, use `Fill` and `BackgroundColor` properties or apply CSS class directly (same verification pattern as 15-6's FluentTabs API note).
  - [ ] 1.5 `OnInitializedAsync`: subscribe to `DashboardRefreshService.OnDataChanged` first, then call `AdminStreamApiClient.GetSystemHealthAsync()`. Set `_isLoading = false` after data loads. **Note:** If the refresh timer fires during initial load, the handler may update state concurrently. This is safe because both paths write the same data shape and `InvokeAsync` serializes UI updates. No lock needed — just ensure `_isLoading` is cleared only after the initial `GetSystemHealthAsync` completes.
  - [ ] 1.6 State management: `_isLoading`, `_apiUnavailable`, `_isStale`, `_healthReport` (current), `_cachedHealthReport` (last-good), `_lastRefreshUtc`. On successful load: `_healthReport = data`, `_cachedHealthReport = data`, `_apiUnavailable = false`, `_isStale = false`, `_lastRefreshUtc = DateTimeOffset.UtcNow`. On null data: `_isStale = true`, `_apiUnavailable = _cachedHealthReport is null`.
  - [ ] 1.7 Error state: `IssueBanner` when `_apiUnavailable && _cachedHealthReport is null`. Stale banner when `_isStale && _cachedHealthReport is not null`.
  - [ ] 1.8 Refresh handler: subscribe `DashboardRefreshService.OnDataChanged += OnRefreshSignal`. In handler: extract `DashboardData.Health`, update state, call `InvokeAsync(StateHasChanged)`. Handle `ObjectDisposedException`.
  - [ ] 1.9 `IAsyncDisposable.DisposeAsync`: unsubscribe from `OnDataChanged`, set `_disposed = true`.
  - [ ] 1.10 Responsive layout: `FluentGrid` with `Spacing="3"` for stat cards. `FluentGridItem xs="6" sm="6" md="3"` for each card (2-col on narrow, 4-col on wide).
  - [ ] 1.11 **Checkpoint**: Page loads health data, shows stat cards and status badge.

- [ ] **Task 2: DAPR component grid** (AC: 3, 10)
  - [ ] 2.1 Add `FluentDataGrid<DaprComponentHealth>` section below stat cards. Columns: ComponentName (monospace CSS class, `PropertyColumn` sortable), ComponentType (`PropertyColumn` sortable), Status (`TemplateColumn` with `StatusBadge` — map HealthStatus: Healthy→success, Degraded→warning, Unhealthy→error — **must include `SortBy="@(items => items.OrderByDescending(x => (int)x.Status))"` parameter** since TemplateColumn has no automatic sort), LastCheckUtc (`TemplateColumn` showing relative time, with `SortBy` on `LastCheckUtc` property).
  - [ ] 2.2 Use `StatusDisplayConfig.FromHealthStatus()` factory method. Add this factory to `StatusBadge.razor`'s existing pattern if not present, or create inline mapping in Health.razor.
  - [ ] 2.3 Default sort: Status descending (unhealthy components surface first). Secondary sort: ComponentName ascending.
  - [ ] 2.4 Grid `aria-label="DAPR component health status"`. Empty state: `EmptyState` with "No DAPR components detected" when `DaprComponents` is empty.
  - [ ] 2.5 Time formatting helper for LastCheckUtc: "just now" (<60s), "N min ago" (<60m), "N hours ago" (<24h), then date. Check `Index.razor` and `Projections.razor` for existing inline time formatting logic before creating a new helper. If found, extract to a shared static method; if not, create one in Health.razor's `@code` block.
  - [ ] 2.6 **Checkpoint**: DAPR grid renders with all columns, status badges, and relative times.

- [ ] **Task 3: Observability deep-link buttons** (AC: 4, 10)
  - [ ] 3.1 Add "Observability Tools" section below DAPR grid. Show only if at least one URL is non-null. Use `FluentStack` horizontal layout for buttons.
  - [ ] 3.2 "View Traces" link button: anchor element (`<a href="@url" target="_blank" rel="noopener noreferrer">`) with `Icons.Regular.Size20.BranchFork` icon. Use `FluentButton As="a"` if supported, or plain `<a>` with outline button CSS class. Visible only if `ObservabilityLinks.TraceUrl` is not null/empty.
  - [ ] 3.3 "View Metrics" link button: `Icons.Regular.Size20.ChartMultiple` icon. Same anchor pattern as traces. Visible if `MetricsUrl` is not null/empty.
  - [ ] 3.4 "View Logs" link button: `Icons.Regular.Size20.DocumentText` icon. Same anchor pattern. Visible if `LogsUrl` is not null/empty.
  - [ ] 3.5 Each link: `aria-label` (e.g., "Open traces in external observability tool"), `target="_blank"`, `rel="noopener noreferrer"`. Styled as outline buttons in a horizontal stack with gap. No JS interop — pure anchor elements for proper browser behavior.
  - [ ] 3.6 **Checkpoint**: Deep-link buttons render conditionally based on available URLs.

- [ ] **Task 4: Unit tests (bUnit)** (AC: 1-11)
  - **Mock `AdminStreamApiClient`** — use NSubstitute (already registered in `AdminUITestContext`)
  - **Merge-blocking tests** (must pass):
  - [ ] 4.1 Test `Health` page renders stat cards with correct values from mock `SystemHealthReport` (AC: 2)
  - [ ] 4.2 Test `Health` page renders overall status badge with correct text for each `HealthStatus` value: Healthy, Degraded, Unhealthy (AC: 1)
  - [ ] 4.3 Test `Health` page renders DAPR component grid with all required columns (AC: 3)
  - [ ] 4.4 Test `Health` page shows `IssueBanner` on API failure (null health report) (AC: 8)
  - [ ] 4.5 Test `Health` page shows stale indicator when refresh fails after successful initial load (AC: 9)
  - [ ] 4.6 Test observability deep-link buttons render when URLs are configured (AC: 4)
  - [ ] 4.7 Test observability buttons are hidden when URLs are null (AC: 4)
  - [ ] 4.8 Test refresh handler updates displayed health data when `DashboardRefreshService.OnDataChanged` fires with new `SystemHealthReport` (AC: 5)
  - **Recommended tests**:
  - [ ] 4.9 Test error rate severity mapping: <1% → success, 1-5% → warning, >5% → error (AC: 2)
  - [ ] 4.10 Test DAPR components stat card shows "{healthy}/{total}" format (AC: 2)
  - [ ] 4.11 Test DAPR component status badge renders correct severity for each HealthStatus (AC: 3, 10)
  - [ ] 4.12 Test stat cards show `SkeletonCard` during loading state (AC: 2)
  - [ ] 4.13 Test page shows empty state when DaprComponents list is empty (AC: 3)
  - [ ] 4.14 Test observability section is hidden when all URLs are null (AC: 4)
  - [ ] 4.15 Test responsive layout: stat cards use correct grid spans (AC: 11)

- [ ] **Task 5: Command palette and final polish** (AC: 7, 10)
  - [ ] 5.1 Update `CommandPaletteCatalog.cs`: existing entries are already present (`"Health Dashboard", "/health"` and `"Dead Letters", "/health/dead-letters"`). Add entries:
    - `new("Health", "DAPR Component Status", "/health")`
    - `new("Health", "Observability Tools", "/health")`
    - Note: These entries all point to `/health` intentionally — they exist for discoverability via fuzzy search, not as distinct navigation targets (single-view page, no anchors).
  - [ ] 5.2 Verify NavMenu.razor already has the Health link (it does: `<FluentNavLink Href="/health" Icon="@(new Icons.Regular.Size20.HeartPulse())">Health</FluentNavLink>`). No changes needed.
  - [ ] 5.3 Create `Pages/Health.razor.css` for scoped styles: monospace class for component names, status badge sizing, section spacing.
  - [ ] 5.4 **Checkpoint**: All ACs pass, zero warnings, health page fully functional.

## Dev Notes

### Architecture Compliance

- **ADR-P4**: Admin.UI communicates exclusively via HTTP REST API to Admin.Server. No DaprClient in UI. Reuse existing `AdminStreamApiClient.GetSystemHealthAsync()` — no new API client needed.
- **ADR-P5**: Deep-link to external observability tools instead of replicating their UIs. URLs configured via DAPR config store or environment variables (`ADMIN_TRACE_URL`, `ADMIN_METRICS_URL`, `ADMIN_LOGS_URL`). Missing URLs gracefully hide buttons.
- **SEC-5**: No event payload data in this story — health dashboard contains only system-level metrics and component status. No sensitive data risk.
- **NFR41**: Render health dashboard within 2 seconds on initial load. Single `GetSystemHealthAsync()` call returns all data.
- **NFR45**: Supports concurrent users — scoped services, no shared mutable state.
- **NFR46**: Health data (read) requires `ReadOnly` policy. No write operations in this story.
- **Tenant-agnostic**: Health dashboard is NOT tenant-scoped. `SystemHealthReport` is system-wide. No tenantId parameter on API calls.

### FR75 Scope — Full Implementation

FR75 specifies: "operational health dashboard with event count, throughput, error rate, DAPR component status, and deep links to configured observability tools." This story implements ALL of FR75 for the Blazor UI surface.

FR82 specifies: "Every trace, metric, and log view deep-links to the corresponding detail in the configured external observability tool." This story implements the deep-link buttons per ADR-P5.

### NO New API Client Needed

Unlike stories 15-5 and 15-6 which created new API clients for new controller endpoints, this story reuses the existing `AdminStreamApiClient.GetSystemHealthAsync()` method. The `SystemHealthReport` record already contains ALL data needed:
- `OverallStatus` (HealthStatus enum: Healthy, Degraded, Unhealthy)
- `TotalEventCount` (long)
- `EventsPerSecond` (double)
- `ErrorPercentage` (double)
- `DaprComponents` (IReadOnlyList<DaprComponentHealth>)
- `ObservabilityLinks` (ObservabilityLinks: TraceUrl?, MetricsUrl?, LogsUrl?)

The `DashboardRefreshService` also fetches health data every 30 seconds and publishes via `OnDataChanged(DashboardData)` where `DashboardData.Health` is the `SystemHealthReport`.

### Existing Code to Reuse (DO NOT Recreate)

| What | Where | How |
|------|-------|-----|
| `SystemHealthReport` record | `src/Hexalith.EventStore.Admin.Abstractions/Models/Health/SystemHealthReport.cs` | Fields: OverallStatus, TotalEventCount, EventsPerSecond, ErrorPercentage, DaprComponents, ObservabilityLinks |
| `DaprComponentHealth` record | `src/Hexalith.EventStore.Admin.Abstractions/Models/Health/DaprComponentHealth.cs` | Fields: ComponentName, ComponentType, Status (HealthStatus), LastCheckUtc (DateTimeOffset) |
| `HealthStatus` enum | `src/Hexalith.EventStore.Admin.Abstractions/Models/Health/HealthStatus.cs` | Values: Healthy, Degraded, Unhealthy |
| `ObservabilityLinks` record | `src/Hexalith.EventStore.Admin.Abstractions/Models/Health/ObservabilityLinks.cs` | Fields: TraceUrl?, MetricsUrl?, LogsUrl? (all nullable string) |
| `AdminStreamApiClient.GetSystemHealthAsync()` | `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs:65` | Returns `SystemHealthReport?` from `GET /api/v1/admin/health` |
| `DashboardRefreshService` | `src/Hexalith.EventStore.Admin.UI/Services/DashboardRefreshService.cs` | Polls every 30s. `DashboardData.Health` = `SystemHealthReport?`. Subscribe via `OnDataChanged` |
| `StatCard` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/StatCard.razor` | Parameters: Label, Value, Severity, Title, Subtitle, IsLoading |
| `EmptyState` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/EmptyState.razor` | Parameters: Title, Description, ActionLabel, ActionHref |
| `IssueBanner` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/IssueBanner.razor` | Parameters: Visible, Title, Description, ActionLabel, OnAction |
| `SkeletonCard` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/SkeletonCard.razor` | Loading skeleton placeholder |
| `StatusBadge` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/StatusBadge.razor` | Parameters: Status, Severity, DisplayConfig. Factory: `StatusDisplayConfig.FromStreamStatus()` — add `FromHealthStatus()` |
| `ViewportService` | `src/Hexalith.EventStore.Admin.UI/Services/ViewportService.cs` | `IsWideViewport` boolean for responsive layout (1280px breakpoint) |
| `NavMenu.razor` | `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor` | Already has Health nav link — no changes needed |
| `CommandPaletteCatalog.cs` | `src/Hexalith.EventStore.Admin.UI/Components/CommandPaletteCatalog.cs` | Already has "Health Dashboard" entry — add sub-entries |

### API Endpoints Used

```
# System health report (existing, ReadOnly policy):
GET /api/v1/admin/health → SystemHealthReport
# Contains: OverallStatus, TotalEventCount, EventsPerSecond,
# ErrorPercentage, DaprComponents[], ObservabilityLinks

# DAPR component detail (existing, ReadOnly policy — NOT needed by this story):
GET /api/v1/admin/health/dapr → IReadOnlyList<DaprComponentHealth>
# (SystemHealthReport.DaprComponents already contains this data)
```

**All endpoints are already implemented in `AdminHealthController`.** This story only replaces the placeholder UI — no server-side changes needed.

### Health Status → Severity Mapping

```csharp
static string GetHealthSeverity(HealthStatus status) => status switch
{
    HealthStatus.Healthy => "success",
    HealthStatus.Degraded => "warning",
    HealthStatus.Unhealthy => "error",
    _ => "neutral",
};
```

### Error Rate → Severity Mapping

```csharp
static string GetErrorRateSeverity(double percentage) => percentage switch
{
    < 1.0 => "success",
    < 5.0 => "warning",
    _ => "error",
};
```

### DashboardRefreshService Integration Pattern

```csharp
// In OnInitializedAsync:
RefreshService.OnDataChanged += OnRefreshSignal;
_healthReport = await ApiClient.GetSystemHealthAsync();
// ... set state flags

// Handler:
private async void OnRefreshSignal(DashboardData data)
{
    if (_disposed) return;
    try
    {
        if (data.Health is not null)
        {
            _healthReport = data.Health;
            _cachedHealthReport = data.Health;
            _isStale = false;
            _apiUnavailable = false;
            _lastRefreshUtc = DateTimeOffset.UtcNow;
        }
        else
        {
            _isStale = _cachedHealthReport is not null;
            _apiUnavailable = _cachedHealthReport is null;
        }
        await InvokeAsync(StateHasChanged);
    }
    catch (ObjectDisposedException) { }
}
```

### FluentDataGrid TemplateColumn Sort — Required Pattern

`TemplateColumn` in FluentUI Blazor does NOT sort automatically — it requires an explicit `SortBy` parameter. Without it, clicking the column header does nothing. This is the correct pattern:

```razor
<TemplateColumn Title="Status" SortBy="@(GridSort<DaprComponentHealth>.ByDescending(x => (int)x.Status))">
    <StatusBadge ... />
</TemplateColumn>

<TemplateColumn Title="Last Check" SortBy="@(GridSort<DaprComponentHealth>.ByDescending(x => x.LastCheckUtc))">
    @FormatTimeAgo(context.LastCheckUtc)
</TemplateColumn>
```

Verify the exact `GridSort<T>` API at build time — FluentUI Blazor v4.13.2 may use `GridSort<T>.ByDescending()` or a lambda-based `SortBy` parameter. Check the [FluentUI Blazor DataGrid docs](https://www.fluentui-blazor.net/).

### Deep-Link Security — `rel="noopener noreferrer"`

All external links opening in `target="_blank"` **must** include `rel="noopener noreferrer"` to prevent reverse tabnapping. Use anchor elements (`<a>`), not JS interop `window.open()`, for proper browser behavior (right-click menu, URL hover preview, accessibility, bookmark-ability).

### UX-DR40 Status Color System

- Green (`var(--hexalith-status-success)`): Healthy
- Yellow/Amber (`var(--hexalith-status-warning)`): Degraded
- Red (`var(--hexalith-status-error)`): Unhealthy
- Gray (`var(--hexalith-status-neutral)`): Unknown / No data

### bUnit Test Pattern

Follow `ProjectionsPageTests` pattern exactly:
- Extend `AdminUITestContext`
- Mock `AdminStreamApiClient` via NSubstitute (already registered in base context)
- Override with specific return values using `_mockApiClient.GetSystemHealthAsync(...).Returns(...)`
- Use `Render<Health>()` and `WaitForAssertion()` with 5-second timeout
- Assert via `cut.Markup.ShouldContain(...)`

### Previous Story Intelligence (15-6)

- Tabs pattern with `FluentTabs` works well but is NOT needed here (single-view page)
- Deep-link URL pattern with query params works but is minimal for this page (no tabs/filters)
- `DashboardRefreshService.OnDataChanged` subscription pattern is proven and stable
- bUnit test pattern with `AdminUITestContext` base class provides all needed mocks
- `StatCard` severity mapping via helper methods is the established pattern
- Monospace CSS class for technical identifiers (component names) follows existing convention

### Project Structure Notes

Files to create/modify:
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Pages/Health.razor` (replace placeholder)
- **CREATE**: `src/Hexalith.EventStore.Admin.UI/Pages/Health.razor.css` (scoped styles)
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Components/CommandPaletteCatalog.cs` (add entries)
- **CREATE**: `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/HealthPageTests.cs` (bUnit tests)
- **POSSIBLY MODIFY**: `src/Hexalith.EventStore.Admin.UI/Components/Shared/StatusBadge.razor` (add `FromHealthStatus()` factory if not present)

No new API clients, no new models, no server-side changes.

### References

- [Source: architecture.md — ADR-P5: Observability Integration Strategy]
- [Source: prd.md — FR75: Operational Health Dashboard]
- [Source: prd.md — FR82: Deep Links to External Observability Tools]
- [Source: prd.md — NFR41: Health Dashboard Initial Load <2s]
- [Source: ux-design-specification.md — UX-DR40: Status Color System]
- [Source: ux-design-specification.md — UX-DR47: Observability Deep Links]
- [Source: ux-design-specification.md — D5: Health-First Monitor Direction]
- [Source: Admin.Abstractions/Models/Health/* — DTOs]
- [Source: Admin.Server/Controllers/AdminHealthController.cs — REST API]
- [Source: Admin.UI/Services/AdminStreamApiClient.cs:65 — GetSystemHealthAsync]
- [Source: Admin.UI/Services/DashboardRefreshService.cs — Polling + SignalR refresh]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
