# Story 15.2: Activity Feed & Recent Active Streams

Status: ready-for-dev
Size: Large (multi-session) — ~15 new files, 8 task groups, 14 ACs

## Story

As a **developer or operator using the Hexalith EventStore admin dashboard**,
I want **a live activity feed on the landing page and a data grid of recently active streams with real-time updates, filtering, and pagination**,
so that **I can immediately see system activity, identify active or problematic streams, and navigate to stream detail for investigation**.

## Acceptance Criteria

1. **Landing page StatCards show live data** from Admin.Server REST API (`GET /api/v1/admin/streams`, `GET /api/v1/admin/health`). Four StatCards: Total Events (from `SystemHealthReport.TotalEventCount`), Events/sec (from `EventsPerSecond`), Error Rate (from `ErrorPercentage`), Active Streams (count from `PagedResult<StreamSummary>.TotalCount`). Severity coloring: error rate >1% = Error, >0.1% = Warning, else Success. Loading state shows SkeletonCard placeholders. API failure shows last known values with stale indicator + IssueBanner.
2. **ActivityChart component** renders a CSS-only bar chart showing **active streams per hour** over the last 24 hours (hourly buckets). Data derived from `StreamSummary.LastActivityUtc` grouped into hourly buckets — this shows "streams with activity in this hour", NOT command throughput (no histogram API exists yet). Each bar shows stream count per bucket. Bars are CSS Flexbox columns. Clicked bar navigates to `/streams?start={bucketStart}&end={bucketEnd}`. Each bar has `aria-label="{time range}: {count} active streams"`. Loading state: skeleton bars. No data: EmptyState. Chart title: "Stream Activity (24h)". Respect `prefers-reduced-motion`.
3. **Recent streams FluentDataGrid** on a new `/streams` page (also embedded as a compact 10-row preview on the landing page) displaying `StreamSummary` data from `GET /api/v1/admin/streams`. Columns: Status (`StreamStatus` mapped to StatusBadge), Tenant (monospace, 100px), Domain (monospace, 100px), Aggregate ID (monospace, truncated 8 chars with tooltip + copy), Event Count (right-aligned, semibold), Last Activity (monospace `HH:mm:ss`, 80px), Has Snapshot (icon). Default sort: `LastActivityUtc` descending.
4. **Pagination** — 25 rows/page default on `/streams`, 10 rows in landing page preview. "Page X of Y" below grid with total count. Page encoded in URL query param `?page=2`.
5. **Filter chips** on `/streams` page — Status filter (All, Active, Idle, Tombstoned) as horizontal toggle buttons. Tenant filter: `FluentSelect` dropdown ("All Tenants" + registered tenants). Domain filter: `FluentSelect` dropdown. Filters apply immediately on change (no Apply button). Filter state encoded in URL query params: `?status=active&tenant=acme&domain=banking`.
6. **Row click** navigates to `/streams/{tenantId}/{domain}/{aggregateId}` (stub page for story 15-3).
7. **Real-time updates — polling PRIMARY, SignalR enhancement** — Implement a 30-second polling timer (`PeriodicTimer`) as the primary data refresh mechanism via `DashboardRefreshService`. On each tick: call `GetSystemHealthAsync()` + `GetRecentlyActiveStreamsAsync()`, fire data changed event. Re-entrancy guard: skip tick if previous refresh still in-flight. **Additionally**, subscribe to `EventStoreSignalRClient` for stream activity projection changes — if a SignalR signal is received, it triggers an immediate refresh (bypassing the timer). This dual approach ensures data freshness regardless of whether SignalR projection types are registered server-side. "New row" = a row whose composite key (`$"{TenantId}/{Domain}/{AggregateId}"`) was not in the previous page's `HashSet<string>`. New rows get 150ms fade-in highlight CSS animation. Data updates silently in-place (paginated grid — no scroll-aware badge needed). StatCard severity color transitions via CSS `transition: color 300ms` (no number counter animation). `aria-live="polite"` on StatCard container and grid summary — no custom debounce, rely on browser/screen reader native batching.
8. **SignalR connection** — Register `EventStoreSignalRClient` in DI with hub URL from Aspire service discovery. **IMPORTANT**: Verify which service hosts the projection change hub — it is expected on CommandApi (`https+http://commandapi/hubs/projection-changes`), NOT on Admin.Server. Check `src/Hexalith.EventStore.CommandApi/` for `ProjectionChangedHub` registration. Configure hub URL via `appsettings.json` (`EventStore:SignalR:HubUrl`) with Aspire service discovery override, rather than hardcoding the service name. Auto-reconnect with group rejoin. `IAsyncDisposable` on all subscribing components.
9. **Admin API service layer** — Create `Services/AdminStreamApiClient.cs` calling Admin.Server REST endpoints via the "AdminApi" `HttpClient`. Methods: `GetRecentlyActiveStreamsAsync(...)`, `GetSystemHealthAsync()`. Deserialize `PagedResult<StreamSummary>` and `SystemHealthReport`. Handle 401 (redirect to login), 403 (show forbidden), 503 (show service unavailable via IssueBanner).
10. **NFR41 full validation** — Live data + SignalR updates render within 200ms of signal receipt (measured from SignalR callback to `StateHasChanged()` completion). Shell + skeleton must render within 2 seconds (baseline from story 15-1).
11. **NFR45 concurrent users** — 10 concurrent admin sessions with independent views. No shared mutable state. Each session has its own SignalR subscription and data cache.
12. **Landing page transforms** — Replace simulated 200ms delay and EmptyState placeholder (from story 15-1) with real API calls. Call `GetSystemHealthAsync()` first. **Conditional rendering based on `TotalEventCount`**: (a) When `TotalEventCount > 0`: show StatCards + ActivityChart + compact stream preview grid (request `count=100` max for landing page, NOT 1000). (b) When `TotalEventCount == 0`: show StatCards (all zeros, green severity — positive empty state) + EmptyState "EventStore Admin is running. 0 commands processed. Send your first command via the Admin API." — **skip ActivityChart and stream preview grid entirely** (no point rendering empty chart/grid, saves an HTTP call to `/streams`). (c) When API unreachable: show IssueBanner (now `Visible=true`) with "Admin API Unavailable" + retry button.
13. **Responsive behavior** — FluentDataGrid columns follow UX spec breakpoints. Compact (960px): hide Domain, Has Snapshot. Minimum (<960px): hide Tenant also, show Status + Aggregate ID + Last Activity only. ActivityChart: 24 bars (1h buckets) at Optimal, 12 bars (2h buckets) at Compact, hidden at Minimum (show "N streams active in last 24h" summary text instead).
14. **Accessibility** — FluentDataGrid column headers have `aria-sort`. StatusBadge in grid has `aria-label="Stream status: {status}"`. Skip-to-main still works. ActivityChart hidden data table accessible via screen reader. `forced-colors` media query: grid row selection uses `Highlight` system color, StatusBadge icon-only differentiation.

## Tasks / Subtasks

- [ ] **Task 1: Admin API service layer** (AC: 9, 12)
  - [ ] 1.1 Create `Services/AdminStreamApiClient.cs` — inject `IHttpClientFactory`, get "AdminApi" client. Methods:
    - `GetRecentlyActiveStreamsAsync(string? tenantId, string? domain, int count = 1000, CancellationToken ct = default)` → `PagedResult<StreamSummary>` via `GET /api/v1/admin/streams?tenantId={}&domain={}&count={}`. Note: `AdminStreamsController` accepts `count` only — no `page`/`pageSize`/`offset` params. Client-side pagination via `Skip()`/`Take()` on returned `Items`.
    - `GetSystemHealthAsync(CancellationToken ct)` → `SystemHealthReport` via `GET /api/v1/admin/health`
    - `GetTenantsAsync(CancellationToken ct)` → `IReadOnlyList<TenantSummary>` via `GET /api/v1/admin/tenants` — needed for tenant filter dropdown on `/streams` page. `TenantSummary` already exists in `Admin.Abstractions/Models/Tenants/`. `AdminTenantsController` already serves this endpoint.
  - [ ] 1.2 Handle HTTP errors: 401 → throw `UnauthorizedAccessException`, 403 → throw `ForbiddenAccessException` (create), 503 → throw `ServiceUnavailableException` (create). Wrap JSON deserialization in `try/catch` — on parse failure, return empty `PagedResult` / default `SystemHealthReport` and log error. Do NOT crash the page on contract drift.
  - [ ] 1.3 Create `Services/Exceptions/ForbiddenAccessException.cs` and `Services/Exceptions/ServiceUnavailableException.cs`
  - [ ] 1.4 Register `AdminStreamApiClient` as scoped service in `Program.cs`. Set **5-second timeout** on the "AdminApi" HttpClient: `client.Timeout = TimeSpan.FromSeconds(5)`. Dashboard UX cannot wait 100s (HttpClient default) for a response.
  - [ ] 1.5 **Checkpoint**: Build compiles with zero warnings

- [ ] **Task 2: SignalR integration** (AC: 7, 8, 11)
  - [ ] 2.1 Add `ProjectReference` to `Hexalith.EventStore.SignalR` in Admin.UI `.csproj`
  - [ ] 2.2 Register `EventStoreSignalRClientOptions` in `Program.cs`. Configure hub URL via `appsettings.json` section `EventStore:SignalR:HubUrl` (default: `https+http://commandapi/hubs/projection-changes`). Aspire overrides this via environment variable. **Verify hub host**: check `src/Hexalith.EventStore.CommandApi/` for `MapHub<ProjectionChangedHub>("/hubs/projection-changes")` to confirm CommandApi is the correct service. Add `appsettings.Development.json` entry with localhost fallback.
  - [ ] 2.3 Register `EventStoreSignalRClient` as singleton in DI
  - [ ] 2.4 Start SignalR connection in `Program.cs` after build. **CRITICAL: Wrap in try/catch** — if CommandApi is down, `StartAsync()` will throw and crash Admin.UI on startup. Catch `Exception`, log warning "SignalR connection failed, polling will handle data refresh", continue. SignalR is enhancement only — the app MUST boot without it. Example: `try { await signalRClient.StartAsync(); } catch (Exception ex) { logger.LogWarning(ex, "SignalR unavailable"); }`
  - [ ] 2.5 Add `EventStoreSignalRClient` to `_Imports.razor` usings
  - [ ] 2.6 **Checkpoint**: Build + SignalR client resolves from DI

- [ ] **Task 3: Landing page live data** (AC: 1, 2, 12)
  - [ ] 3.1 Refactor `Pages/Index.razor` — remove simulated 200ms delay. Inject `AdminStreamApiClient`. **IMPORTANT**: Set `_isLoading = true` as default field value so skeleton renders on first paint without waiting for `OnInitializedAsync`. In `OnInitializedAsync`: call `GetSystemHealthAsync(ct)` first. If `TotalEventCount > 0`, then call `GetRecentlyActiveStreamsAsync(null, null, 100, ct)` (4 params — matches method signature, cap at 100 for landing page). If `TotalEventCount == 0`, skip streams call. Set `_isLoading = false` + `StateHasChanged()` after all calls complete. This ensures skeleton cards appear immediately (NFR41 2s budget).
  - [ ] 3.2 Wire StatCards to live data: Total Events → `SystemHealthReport.TotalEventCount`, Events/sec → `EventsPerSecond` (formatted `{N:F1}/s`), Error Rate → `ErrorPercentage` (formatted `{N:F2}%`), Active Streams → `PagedResult.TotalCount` (or "0" if `TotalEventCount == 0` — streams call is skipped, so hardcode 0). Severity: error >1% → Error, >0.1% → Warning, else Success. **Stale data handling**: cache last-good StatCard values in component fields. On API timeout/failure: render cached values with `opacity: 0.5` CSS class + "(stale)" label suffix on each StatCard. On next successful refresh: remove stale indicator, update cache.
  - [ ] 3.3 Create `Components/ActivityChart.razor` — CSS Flexbox bar chart titled "Stream Activity (24h)". Parameters: `IReadOnlyList<ActivityBucket>` where `ActivityBucket(DateTimeOffset Start, DateTimeOffset End, int StreamCount)`. Each bar: `height` proportional to max bucket count. Single color (accent blue) — no success/failure split since data is stream activity count, not command outcomes. Click handler → `NavigationManager.NavigateTo($"/streams?start={}&end={}")`. Each bar has `aria-label="{time range}: {count} active streams"` AND `title="{HH:mm}-{HH:mm}: {count} streams"` for native browser hover tooltip. Skeleton state. `prefers-reduced-motion`: no animation. Build bucket data by grouping `StreamSummary.LastActivityUtc` into hourly slots.
  - [ ] 3.4 Create `Models/ActivityBucket.cs` record in `Components/` folder (UI-only model, not in Abstractions): `record ActivityBucket(DateTimeOffset Start, DateTimeOffset End, int StreamCount)`
  - [ ] 3.5 Wire landing page with **conditional rendering**: (a) Always show StatCards (SkeletonCards during load → real values). (b) If `TotalEventCount > 0`: render ActivityChart below stats + compact 10-row stream preview grid below chart. If all ActivityChart buckets are zero (streams exist but none had activity in last 24h), show "No stream activity in the last 24 hours" EmptyState instead of 24 flat bars. (c) If `TotalEventCount == 0`: render StatCards (all zeros, green severity) + EmptyState below — **skip ActivityChart and stream grid entirely** (avoids empty chart noise + saves HTTP call). When API fails: activate IssueBanner `Visible=true` with retry button calling `LoadDataAsync()`.
  - [ ] 3.6 Handle empty data state: if `TotalEventCount == 0`, show EmptyState with "EventStore Admin is running. 0 commands processed. Send your first command via the Admin API." Do NOT call `GetRecentlyActiveStreamsAsync` — there are no streams to fetch.
  - [ ] 3.7 Handle API unavailable: catch `ServiceUnavailableException` → set IssueBanner Visible=true, Title="Admin API Unavailable", Description="Cannot reach admin-server. Check Aspire topology.", ActionLabel="Retry". **Handle partial failure**: if `GetSystemHealthAsync` succeeds but `GetRecentlyActiveStreamsAsync` fails, show StatCards normally + show EmptyState with error message in the streams preview area ("Stream data temporarily unavailable"). Don't blank the whole page for a partial failure.

- [ ] **Task 4: Streams page with FluentDataGrid** (AC: 3, 4, 5, 6, 13)
  - [ ] 4.1 Create `Pages/Streams.razor` at route `/streams` — inject `AdminStreamApiClient`, `NavigationManager`
  - [ ] 4.2 FluentDataGrid bound to `IQueryable<StreamSummary>` with columns per AC3. StatusBadge component for `StreamStatus`. Monospace CSS class for Tenant, Domain, AggregateId. AggregateId truncated to 8 chars with `title` tooltip + copy-to-clipboard icon on hover. Event Count right-aligned semibold. Last Activity formatted `HH:mm:ss`. Has Snapshot as FluentIcon (checkmark/empty).
  - [ ] 4.3 Pagination: FluentPaginator with 25 rows/page default. "Page {current} of {total}" display. URL sync: read `?page=` param on init, update URL on page change via `NavigationManager.NavigateTo(..., forceLoad: false, replace: true)`.
  - [ ] 4.4 Filter chips: Create `Components/StreamFilterBar.razor` — Status chips (All, Active, Idle, Tombstoned) using `FluentButton Appearance.Accent/Outline`. Tenant `FluentSelect` ("All Tenants" + list). Domain `FluentSelect`. All filters encoded in URL query params. Filters apply immediately — each change triggers `LoadStreamsAsync()`.
  - [ ] 4.5 Row click: `@onclick` navigates to `/streams/{tenantId}/{domain}/{aggregateId}`.
  - [ ] 4.6 Create stub page `Pages/StreamDetail.razor` at route `/streams/{TenantId}/{Domain}/{AggregateId}` with EmptyState "Stream detail view coming in story 15-3."
  - [ ] 4.7 Responsive column hiding: use CSS `display: none` at compact/minimum breakpoints per UX spec.
  - [ ] 4.8 Add "Streams" to sidebar navigation in `NavMenu.razor` (icon: `FluentIcon` list/stream intent, route: `/streams`, min role: ReadOnly). Insert between "Events" and "Health".

- [ ] **Task 5: Real-time updates — polling primary, SignalR enhancement** (AC: 7, 10, 11)
  - [ ] 5.1 Create `Services/DashboardRefreshService.cs` implementing `IAsyncDisposable` — 30-second polling via `PeriodicTimer(TimeSpan.FromSeconds(30))`. On each tick: call `AdminStreamApiClient.GetSystemHealthAsync()` + `GetRecentlyActiveStreamsAsync()`, fire `OnDataChanged` event with new data. **Re-entrancy guard**: use `bool _isRefreshing` flag — if previous tick hasn't completed, skip current tick. No data diffing — fire event every tick, let Blazor's diffing handle unchanged DOM. **Error handling in timer loop**: wrap API calls in try/catch. On failure: fire `OnError` event (components show IssueBanner/stale indicator), do NOT crash the timer loop. No exponential backoff — always poll at 30s (admin tooling, max 10 users — backoff is over-engineering). Inject as scoped service. Expose `TriggerImmediateRefreshAsync()` for SignalR to call. **DisposeAsync**: stop `PeriodicTimer`, cancel in-flight `CancellationTokenSource`, clean up.
  - [ ] 5.2 Landing page: inject `DashboardRefreshService`, subscribe to `OnDataChanged`. On change → update StatCards + stream preview grid via `InvokeAsync()`. Track previous stream composite keys in `HashSet<string>` keyed on `$"{TenantId}/{Domain}/{AggregateId}"` to detect new rows for fade-in highlight.
  - [ ] 5.3 **SignalR enhancement layer**: Additionally subscribe to `EventStoreSignalRClient` for `stream-activity` and `system-health` projections. On signal → call `DashboardRefreshService.TriggerImmediateRefreshAsync()` (resets the 30s timer). If SignalR subscription silently fails (projection types not registered), polling continues unaffected. Log warning on subscribe failure, not error.
  - [ ] 5.4 Streams page: subscribe to `DashboardRefreshService.OnDataChanged` as a **signal only** — do NOT consume the service's unfiltered data directly. On signal: re-fetch via `AdminStreamApiClient.GetRecentlyActiveStreamsAsync(currentTenantFilter, currentDomainFilter, 1000, ct)` with the page's current filter state. This prevents polling from overwriting the user's filtered view with unfiltered data. New rows: add CSS `@keyframes` fade-in highlight (150ms, accent color at 10% opacity). Data silently updates in-place on current page.
  - [ ] 5.5 StatCard severity color: add CSS `transition: color 300ms ease-out` on `.stat-card-value` for smooth severity color changes. No number counter animation — just update the number directly. Blazor re-renders handle it.
  - [ ] 5.6 Accessibility: set `aria-live="polite"` on StatCard container region and grid summary. No custom debounce timer — rely on browser/screen reader native batching of polite announcements. If screen reader flooding is observed during testing, add debounce in a follow-up.
  - [ ] 5.7 Wire `HeaderStatusIndicator.ConnectionStatus` from `EventStoreSignalRClient` connection state. When SignalR is connected → `ConnectionStatus.Connected` (green). When SignalR disconnects but polling works → `ConnectionStatus.Unknown` (gray). When both SignalR and polling fail → `ConnectionStatus.Disconnected` (red). Listen to `DashboardRefreshService.OnError` to detect polling failures.
  - [ ] 5.8 `IAsyncDisposable` on all subscribing pages/components: unsubscribe from SignalR + dispose timer on dispose.

- [ ] **Task 6: Update existing navigation and routes** (AC: 6, 8, 12)
  - [ ] 6.1 Update `Components/CommandPalette.razor` — add "Streams", "Recent Activity" to placeholder search results. Selecting navigates to `/streams`.
  - [ ] 6.2 Landing page preview grid: "View all streams →" link below the 10-row preview navigating to `/streams`
  - [ ] 6.3 StatCard click navigation: clicking "Active Streams" StatCard navigates to `/streams?status=active`, clicking "Error Rate" navigates to `/health`
  - Note: NavMenu "Streams" link already added in Task 4.8 — do NOT duplicate.

- [ ] **Task 7: Unit tests (bUnit)** (AC: 1, 2, 3, 4, 5, 7, 9, 13, 14)
  - **Mock AdminStreamApiClient and SignalR** — use NSubstitute for `AdminStreamApiClient`, mock `EventStoreSignalRClient` subscriptions
  - **Merge-blocking tests** (must pass):
  - [ ] 7.1 Test landing page renders StatCards with data from mocked health API (AC: 1)
  - [ ] 7.2 Test landing page shows SkeletonCards during loading (AC: 1)
  - [ ] 7.3 Test landing page shows IssueBanner when API unavailable (AC: 12)
  - [ ] 7.4 Test landing page shows EmptyState when `TotalEventCount == 0` — no ActivityChart or grid rendered (AC: 12)
  - [ ] 7.5 Test landing page shows stale data with opacity + "(stale)" label on API timeout (AC: 1)
  - [ ] 7.6 Test Streams page FluentDataGrid renders all columns with correct data (AC: 3)
  - [ ] 7.7 Test StatusBadge maps StreamStatus correctly (Active=green, Idle=gray, Tombstoned=red) (AC: 3)
  - **Recommended tests** (important, implement after blocking tests):
  - [ ] 7.8 Test ActivityChart renders correct number of bars with correct aria-labels AND title tooltips (AC: 2)
  - [ ] 7.9 Test ActivityChart shows EmptyState when all buckets have zero StreamCount (AC: 2)
  - [ ] 7.10 Test Streams page pagination: page change triggers reload + URL update (AC: 4)
  - [ ] 7.11 Test filter chips: changing status filter reloads data with correct parameters (AC: 5)
  - [ ] 7.12 Test row click navigates to `/streams/{tenantId}/{domain}/{aggregateId}` (AC: 6)
  - [ ] 7.13 Test responsive: verify column visibility classes at different breakpoint simulations (AC: 13)
  - [ ] 7.14 Test DashboardRefreshService polling triggers data reload via `OnDataChanged` event (AC: 7)

- [ ] **Task 8: E2E validation** (AC: 10, 14)
  - [ ] 8.1 Playwright test: landing page loads StatCards within 2 seconds (NFR41 baseline)
  - [ ] 8.2 Playwright test: Streams page loads and displays grid with pagination
  - [ ] 8.3 Accessibility: run axe-core on `/streams` page, assert zero violations
  - [ ] 8.4 High-contrast: screenshot `/streams` in forced-colors mode for manual review
  - [ ] 8.5 Keyboard: Tab through filter chips → grid → pagination controls in correct order

## Dev Notes

### Architecture Compliance

- **ADR-P4 (continued)**: Admin.UI communicates exclusively via HTTP REST API to Admin.Server. No DaprClient in UI. The `AdminStreamApiClient` service wraps `HttpClient` calls to `GET /api/v1/admin/streams` and `GET /api/v1/admin/health`.
- **ADR-P5**: Observability deep-links are NOT wired in this story. The `SystemHealthReport.ObservabilityLinks` data is fetched but not displayed until story 15-7.
- **SignalR hub path**: `/hubs/projection-changes` on CommandApi, not on Admin.Server. The projection change hub is on the CommandApi service (which processes events), so the UI subscribes directly to CommandApi's SignalR hub for stream activity signals.

### Existing Code to Reuse (DO NOT Recreate)

| What | Where | How |
|------|-------|-----|
| `StreamSummary` record | `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/StreamSummary.cs` | Direct use in FluentDataGrid binding |
| `StreamStatus` enum | `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/StreamStatus.cs` | Map to StatusBadge (Active/Idle/Tombstoned) |
| `PagedResult<T>` record | `src/Hexalith.EventStore.Admin.Abstractions/Models/Common/PagedResult.cs` | Deserialize API responses |
| `SystemHealthReport` record | `src/Hexalith.EventStore.Admin.Abstractions/Models/Health/SystemHealthReport.cs` | StatCard data source |
| `IStreamQueryService` interface | `src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs` | Match API client methods to service interface |
| `AdminStreamsController` | `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` | Verify endpoint signatures match client calls |
| `EventStoreSignalRClient` | `src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs` | Inject and use directly — subscribe/unsubscribe pattern |
| `CounterHistoryGrid.razor` | `samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterHistoryGrid.razor` | Reference for SignalR + FluentDataGrid integration pattern |
| `StatusBadge.razor` | `src/Hexalith.EventStore.Admin.UI/Components/Shared/StatusBadge.razor` | Extend to support `StreamStatus` enum (currently only maps `CommandStatus`) |
| `StatCard.razor` | `src/Hexalith.EventStore.Admin.UI/Components/Shared/StatCard.razor` | Already exists from story 15-1 — wire to real data |
| `SkeletonCard.razor` | `src/Hexalith.EventStore.Admin.UI/Components/Shared/SkeletonCard.razor` | Already exists — used during loading |
| `EmptyState.razor` | `src/Hexalith.EventStore.Admin.UI/Components/Shared/EmptyState.razor` | Already exists — used for zero-data state |
| `IssueBanner.razor` | `src/Hexalith.EventStore.Admin.UI/Components/Shared/IssueBanner.razor` | Already exists with `Visible=false` — activate when API fails |
| `HeaderStatusIndicator.razor` | `src/Hexalith.EventStore.Admin.UI/Components/Shared/HeaderStatusIndicator.razor` | Wire `ConnectionStatus` enum from SignalR connection state |
| `AdminApiAuthorizationHandler` | `src/Hexalith.EventStore.Admin.UI/Services/AdminApiAuthorizationHandler.cs` | Already registered — HttpClient "AdminApi" uses it |
| `AdminUserContext` | `src/Hexalith.EventStore.Admin.UI/Services/AdminUserContext.cs` | Already registered — use for role checks |
| `MainLayout.razor` | `src/Hexalith.EventStore.Admin.UI/Layout/MainLayout.razor` | Already exists — no modifications needed |

### StatusBadge Extension — Generic Approach (Preferred)

Story 15-1 created StatusBadge for `CommandStatus` enum. This story needs it to also render `StreamStatus`. **Use the generic approach**: refactor StatusBadge to accept a display configuration record (`StatusDisplayConfig`) containing icon, color, and label — then create static mapping methods for each enum type. This avoids duplicating the component and scales to future status enums (e.g., `ProjectionStatus` in story 15-5).

**Implementation pattern:**
- Add `[Parameter] public StatusDisplayConfig? DisplayConfig { get; set; }` alongside existing `CommandStatus` parameter
- Create `StatusDisplayConfig` record: `(string Label, string IconName, string CssClass)`
- Add static helper: `StatusDisplayConfig.FromStreamStatus(StreamStatus status)` returning the config
- Keep existing `CommandStatus` parameter working for backward compatibility

| StreamStatus | Color | Icon Intent | Label |
|-------------|-------|-------------|-------|
| Active | Green | Activity/pulse | Active |
| Idle | Gray/Neutral | Pause/idle | Idle |
| Tombstoned | Red (muted) | Archive/tombstone | Tombstoned |

Do NOT create a separate `StreamStatusBadge` component — one component, multiple enum mappings.

### FluentDataGrid Column Spec (Streams Page)

| Column | Property | Alignment | Font | Width | Responsive |
|--------|----------|-----------|------|-------|------------|
| Status | `StreamStatus` | Left | StatusBadge | 100px fixed | Always visible |
| Tenant | `TenantId` | Left | Monospace | 100px fixed | Hidden <960px |
| Domain | `Domain` | Left | Monospace | 100px fixed | Hidden <1280px |
| Aggregate ID | `AggregateId` | Left | Monospace, truncated 8 chars | Min 140px | Always visible |
| Events | `EventCount` | Right | Regular, semibold | 80px fixed | Always visible |
| Last Activity | `LastActivityUtc` | Left | Monospace `HH:mm:ss` | 80px fixed | Always visible |
| Snapshot | `HasSnapshot` | Center | Icon (checkmark/dash) | 48px fixed | Hidden <1280px |

### Data Flow Mapping (Which API Feeds Which Component)

| Component | API Source | Endpoint | Notes |
|-----------|-----------|----------|-------|
| StatCard: Total Events | `SystemHealthReport.TotalEventCount` | `GET /api/v1/admin/health` | Always fetched first |
| StatCard: Events/sec | `SystemHealthReport.EventsPerSecond` | `GET /api/v1/admin/health` | Same response |
| StatCard: Error Rate | `SystemHealthReport.ErrorPercentage` | `GET /api/v1/admin/health` | Same response |
| StatCard: Active Streams | `PagedResult<StreamSummary>.TotalCount` | `GET /api/v1/admin/streams?count=100` | Only if TotalEventCount > 0 |
| ActivityChart | Derived from `StreamSummary.LastActivityUtc` | `GET /api/v1/admin/streams?count=100` | Group into hourly buckets client-side |
| Landing preview grid | `PagedResult<StreamSummary>.Items[0..9]` | `GET /api/v1/admin/streams?count=100` | First 10 items from same response |
| Streams page grid | `PagedResult<StreamSummary>.Items` | `GET /api/v1/admin/streams?count=1000` | Client-side pagination with Skip/Take |
| HeaderStatusIndicator | SignalR `HubConnectionState` | N/A (local state) | Connected/Disconnected/Unknown |
| StreamFilterBar: Tenant dropdown | `IReadOnlyList<TenantSummary>` | `GET /api/v1/admin/tenants` | Fetched once on Streams page init |

### API Call Patterns

```
# Landing page (sequential — health first, then conditional):
GET /api/v1/admin/health              → SystemHealthReport (StatCards)
# Only if TotalEventCount > 0:
GET /api/v1/admin/streams?count=100   → PagedResult<StreamSummary> (ActivityChart + preview grid)

# Streams page (on init):
GET /api/v1/admin/tenants             → IReadOnlyList<TenantSummary> (tenant filter dropdown)
GET /api/v1/admin/streams?tenantId={}&domain={}&count=1000
                                      → PagedResult<StreamSummary> (full grid, client-side pagination)
```

**Pagination strategy (v1 — client-side):** The Admin.Server `GET /api/v1/admin/streams` endpoint accepts `count` param (max items) but not `page`/`pageSize`. For v1 admin tooling with typical stream counts (<10K), request `count=1000` and paginate client-side in the UI using `Skip()`/`Take()` on the returned `Items` list. This is acceptable for the admin dashboard use case. If `PagedResult.ContinuationToken` is non-null, use it for cursor-based next-page fetching. **Future scale note**: If stream counts exceed 10K, a server-side pagination endpoint (`offset`/`limit` params) should be added to `AdminStreamsController`. Do NOT over-engineer pagination for v1.

### ActivityChart Technical Notes

- CSS-only implementation (no JavaScript charting library) per UX spec
- Each bar is a `div` with `display: flex; flex-direction: column-reverse;` — single color (accent blue), NOT stacked success/failure
- Bar height proportional to max bucket count (normalize to percentage of container height)
- 24 bars at full width (Optimal/Standard), 12 bars (2h buckets) at Compact, hidden at Minimum
- **Data source**: Group `StreamSummary.LastActivityUtc` from the streams API response into hourly buckets. Each bucket counts "number of streams whose last activity falls in this hour." This is NOT command throughput — label the chart "Stream Activity (24h)" accordingly.
- **Why not command volume?** No dedicated histogram/throughput API endpoint exists. `SystemHealthReport.EventsPerSecond` gives current rate but no historical breakdown. A `GET /api/v1/admin/health/histogram` endpoint can be added in a future backend story to enable true command volume charting.
- Click navigates to `/streams?start={}&end={}` (filtered stream list), NOT `/commands?start=...`

### SignalR Subscription Strategy

```
// Landing page subscriptions:
SignalRClient.SubscribeAsync("stream-activity", "all", OnStreamActivityChanged)
SignalRClient.SubscribeAsync("system-health", "all", OnHealthChanged)

// Streams page subscription:
SignalRClient.SubscribeAsync("stream-activity", currentTenantFilter ?? "all", OnStreamActivityChanged)
```

**NOTE**: The projection type names (`stream-activity`, `system-health`) must match what the server-side projection builder publishes via the SignalR hub. Before implementing SignalR subscriptions, grep the codebase for these strings to verify they exist. If missing, SignalR subscriptions will silently receive no signals — which is fine because **polling is the primary refresh mechanism** and SignalR is an optimization layer.

### Refresh Architecture (Polling Primary, SignalR Enhancement)

The `DashboardRefreshService` encapsulates the dual-layer refresh strategy:

1. **Primary layer — 30-second polling** (`PeriodicTimer`):
   - Always active, always works regardless of SignalR projection registration
   - On each tick: call `GetSystemHealthAsync()` + `GetRecentlyActiveStreamsAsync()`, fire `OnDataChanged` event
   - No data diffing — fire every tick, let Blazor's DOM diffing handle unchanged content
   - Re-entrancy guard: `bool _isRefreshing` — skip tick if previous still in-flight
   - Error handling: catch exceptions in timer loop, fire `OnError` event, never crash. No backoff — always 30s.
   - `IAsyncDisposable`: stop timer + cancel in-flight CTS on dispose (Blazor Server disposes scoped services on circuit teardown)

2. **Enhancement layer — SignalR signal-triggered immediate refresh**:
   - Subscribe to `stream-activity` and `system-health` projection types
   - On signal receipt: call `TriggerImmediateRefreshAsync()` (resets the 30s timer)
   - If SignalR projections don't exist server-side, subscriptions receive nothing — polling handles it

### Error Handling Strategy

| HTTP Status | UI Behavior |
|-------------|------------|
| 200 | Render data normally |
| 401 | Redirect to login page or show "Session expired. Please log in again." |
| 403 | Show "Access denied. Insufficient permissions to view streams." EmptyState |
| 404 | Show "No data found" EmptyState |
| 503 | Activate IssueBanner: "Admin API Unavailable" with retry button |
| Network error | Activate IssueBanner: "Connection lost." + retry button. Polling continues at 30s interval — next success auto-clears the banner. |

### Code Patterns to Follow

1. **File-scoped namespaces** (`namespace Hexalith.EventStore.Admin.UI.Services;`)
2. **Allman brace style**
3. **Private fields**: `_camelCase`
4. **4-space indentation**, CRLF, UTF-8
5. **Nullable enabled**, **implicit usings enabled**
6. **Primary constructors** for services
7. **ConfigureAwait(false)** on all async calls
8. **CancellationToken** parameter on all async methods
9. **`IAsyncDisposable`** on all components with SignalR subscriptions

### File Structure (New/Modified Files)

```
src/Hexalith.EventStore.Admin.UI/
  Program.cs                               (MODIFY: add SignalR + AdminStreamApiClient DI)
  _Imports.razor                           (MODIFY: add SignalR usings)
  Services/
    AdminStreamApiClient.cs                (NEW: HTTP client for streams + health API)
    DashboardRefreshService.cs             (NEW: 30s polling timer + SignalR trigger integration)
    Exceptions/
      ForbiddenAccessException.cs          (NEW)
      ServiceUnavailableException.cs       (NEW)
  Components/
    ActivityChart.razor                    (NEW: CSS bar chart)
    ActivityChart.razor.css                (NEW: chart styles)
    StreamFilterBar.razor                  (NEW: filter chips for streams page)
    Shared/
      StatusBadge.razor                    (MODIFY: add StreamStatus support)
  Models/
    ActivityBucket.cs                      (NEW: UI-only model for chart data)
  Pages/
    Index.razor                            (MODIFY: live data, ActivityChart, stream preview)
    Streams.razor                          (NEW: /streams page with FluentDataGrid)
    StreamDetail.razor                     (NEW: /streams/{t}/{d}/{id} stub page)
  Layout/
    NavMenu.razor                          (MODIFY: add Streams link)
  wwwroot/
    css/
      app.css                              (MODIFY: add grid responsive rules, chart styles)
    js/
      interop.js                           (NO CHANGES needed for this story — scroll detection removed)

tests/Hexalith.EventStore.Admin.UI.Tests/
  Pages/
    IndexPageTests.cs                      (NEW or MODIFY)
    StreamsPageTests.cs                    (NEW)
  Components/
    ActivityChartTests.cs                  (NEW)
    StreamFilterBarTests.cs                (NEW)
    StatusBadgeStreamTests.cs              (NEW)
```

### Anti-Patterns to Avoid

- Do NOT add DaprClient to Admin.UI — use `AdminStreamApiClient` calling REST API only
- Do NOT reference Admin.Server project — only Admin.Abstractions for DTOs
- Do NOT create custom grid components — use `FluentDataGrid` with `PropertyColumn`/`TemplateColumn`
- Do NOT use JavaScript charting libraries — ActivityChart is CSS-only
- Do NOT poll on every render cycle — use SignalR signals or timer-based polling with minimum 30s interval
- Do NOT log JWT tokens or user data in console
- Do NOT hardcode Admin.Server URL — use Aspire service discovery via HttpClientFactory
- Do NOT use `.sln` files — use `.slnx` only
- Do NOT skip `ConfigureAwait(false)` on async calls
- Do NOT create new packages not in `Directory.Packages.props`
- Do NOT add FluentDataGrid high-contrast CSS for features not in this story (defer to story adding that feature)

### Out of Scope (Deferred)

| Deferred Item | Deferred To |
|---------------|-------------|
| Stream detail page (timeline, event detail) | Story 15-3 |
| Aggregate state inspector + diff viewer | Story 15-4 |
| Observability deep-link buttons | Story 15-7 |
| Topology sidebar tree population | Story 15-3+ |
| Compact density toggle UI | Future (pagination works, toggle deferred) |
| ActivityChart: dedicated histogram API endpoint | Future backend story |
| Full causation chain tracing in UI | Story 15-3 / Epic 20 |

### Known Gaps (Not Blockers)

- **Incident triage**: `StreamSummary` has no `ErrorCount` or `LastErrorUtc` fields. Operators cannot filter streams by "has errors." They can sort by `LastActivityUtc` and scan `StreamStatus`, but there's no error-count-based view. **Recommendation**: Add `ErrorCount` and `LastErrorUtc` to `StreamSummary` in a future backend story to enable error-based filtering and sorting.
- **Domain-scoped dashboard**: Landing page StatCards always show global metrics. No tenant/domain filtering on the landing page. Acceptable for v1 — the `/streams` page has full filtering. Domain-scoped dashboard can be added when the topology sidebar tree is populated (story 15-3+).

### Dev Verification (Test Data Seeding)

To test with live data during development:
1. Run the AppHost (`dotnet run --project src/Hexalith.EventStore.AppHost/`)
2. Open the Counter sample Swagger UI and send 5+ counter increment commands across different tenants
3. Verify `GET /api/v1/admin/health` returns a `SystemHealthReport` with `TotalEventCount > 0`
4. Verify `GET /api/v1/admin/streams?count=10` returns a non-empty `PagedResult<StreamSummary>`
5. Only then wire and verify the UI components against live data

If Admin.Server endpoints return 503 or empty results, check that DAPR sidecars are running and the `admin:stream-activity:*` state store keys are populated.

### Previous Story Intelligence (15-1)

Key patterns established by story 15-1:
- `MainLayout.razor` uses `FluentLayout` + `FluentHeader` + `FluentBodyContent` with sidebar collapse via Ctrl+B
- JS interop module at `wwwroot/js/interop.js` with `hexalithAdmin` namespace for localStorage + keyboard shortcuts
- `StatusBadge.razor`, `StatCard.razor`, `SkeletonCard.razor`, `EmptyState.razor`, `IssueBanner.razor`, `HeaderStatusIndicator.razor` all exist as shell components
- Auth handler (`AdminApiAuthorizationHandler`) and user context (`AdminUserContext`) already registered
- bUnit test project at `tests/Hexalith.EventStore.Admin.UI.Tests/` already exists
- Responsive breakpoints defined in `app.css` (Optimal 1920px+, Standard 1280px, Compact 960px, Minimum <960px)
- Semantic color tokens defined as CSS custom properties
- `forced-colors` media query base rules exist in `app.css`

### References

- [Source: _bmad-output/planning-artifacts/prd.md — FR68 (recently active streams listing)]
- [Source: _bmad-output/planning-artifacts/architecture.md — ADR-P4, ADR-P5]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR41-DR49, FluentDataGrid conventions, real-time update patterns, activity feed patterns, StatusBadge/StatCard live data specs]
- [Source: _bmad-output/planning-artifacts/epics.md — Epic 15 summary]
- [Source: _bmad-output/implementation-artifacts/15-1-blazor-shell-fluentui-layout-command-palette-dark-mode.md — previous story context]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs — API contract]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/ — DTOs]
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs — REST endpoints]
- [Source: src/Hexalith.EventStore.SignalR/EventStoreSignalRClient.cs — SignalR client pattern]
- [Source: samples/Hexalith.EventStore.Sample.BlazorUI/Components/CounterHistoryGrid.razor — FluentDataGrid + SignalR pattern]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
