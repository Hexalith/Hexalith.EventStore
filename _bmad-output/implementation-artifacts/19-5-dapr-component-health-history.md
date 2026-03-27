# Story 19.5: DAPR Component Health History

Status: done

Size: Medium-Large — Creates a `BackgroundService`-based health snapshot collector (`DaprHealthHistoryCollector`), new models (`DaprComponentHealthSnapshot`, `DaprComponentHealthTimeline`, `DaprHealthHistoryEntry`) in Admin.Abstractions, extends `IHealthQueryService` with `GetComponentHealthHistoryAsync`, adds `GET /api/v1/admin/health/dapr/history` endpoint to `AdminHealthController`, creates `AdminHealthHistoryApiClient` UI HTTP client, creates `DaprHealthHistory.razor` dashboard page with CSS-based timeline heatmap + status transition log + per-component drill-down + time-range picker, adds "Health History" button to `DaprComponents.razor`. Creates ~6-8 test classes across 3-4 test projects (~30-40 tests). Completes Epic 19's DAPR Infrastructure Visibility suite.

**Dependency:** Stories 19-1 through 19-4 must be complete or merged (this story reads existing `DaprComponentDetail` health data captured by story 19-1's `IDaprInfrastructureQueryService.GetComponentsAsync()`). Epics 14 and 15 must be complete (both are `done`). Story 15-7 (Health Dashboard) must be complete — this story extends the existing `IHealthQueryService` and `AdminHealthController`.

## Definition of Done

- All acceptance criteria verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- New `/dapr/health-history` page renders with timeline heatmap, transition log, and per-component drill-down
- New REST endpoint returns structured JSON with health history entries for a time range
- Background health snapshot collector persists snapshots every 60 seconds to DAPR state store
- DAPR components page (`/dapr`) includes "Health History" button
- All existing Tier 1 and Tier 2 tests continue to pass with zero behavioral change
- Health history retention cleanup runs automatically based on configured retention period
- Missing or unavailable state store is handled gracefully with EmptyState

## Story

As a **platform operator or DBA using the Hexalith EventStore Admin UI**,
I want **a DAPR component health history page showing a timeline of health status changes for each component over a configurable time window, with a visual heatmap revealing patterns of degradation and recovery, a transition log showing exactly when and how health status changed, and per-component drill-down for focused investigation**,
so that **I can identify recurring failure patterns (e.g., nightly degradation during backup windows), correlate health changes across components to diagnose cascading failures, prove SLA compliance with historical evidence, and perform post-incident forensics without relying solely on external observability tools**.

## Acceptance Criteria

1. **AC1: Background health snapshot collection** — A `BackgroundService` (`DaprHealthHistoryCollector`) runs on Admin.Server startup. After a 15-second initial delay (to allow the DAPR sidecar to initialize), it performs one immediate snapshot capture, then enters a periodic loop every 60 seconds (configurable via `AdminServerOptions.HealthHistoryCaptureIntervalSeconds`, default: 60). Each cycle calls the existing `IDaprInfrastructureQueryService.GetComponentsAsync()` and persists a timestamped snapshot to the DAPR state store. Each snapshot records: component name, component type, health status (`HealthStatus` enum), and capture timestamp (`DateTimeOffset`). If `GetComponentsAsync` returns an empty list (e.g., sidecar unreachable), the collector skips the write for that cycle and logs at Debug level — this avoids polluting history with empty snapshots during sidecar downtime. The collector logs a warning on state store write failure and continues collecting. The collector is enabled by default (`AdminServerOptions.HealthHistoryEnabled`, default: `true`). When disabled, no background collection occurs and the history page shows guidance to enable it.

2. **AC2: State store persistence and key design** — Health snapshots are persisted to the DAPR state store using the configured `AdminServerOptions.StateStoreName`. Key format: `admin:health-history:{yyyyMMdd}` (one key per calendar day UTC). Each key stores a `DaprComponentHealthTimeline` containing a list of `DaprHealthHistoryEntry` records for that day. New entries are appended by reading the current day's key, appending, and writing back. This day-partitioned design enables efficient time-range queries and straightforward retention cleanup. Keys older than `AdminServerOptions.HealthHistoryRetentionDays` (default: 7) are deleted during a daily cleanup pass (runs once at collector startup and once per day thereafter). **Multi-instance note:** The read-modify-write cycle is not concurrency-safe across multiple Admin.Server instances. For v1 (single-instance deployment), this is acceptable. If multi-instance support is needed later, use DAPR's ETag-based optimistic concurrency on `SaveStateAsync` with a single retry on conflict (next cycle is 60 seconds away, so dropping a snapshot is low-cost).

3. **AC3: REST endpoint with time-range filtering** — `GET /api/v1/admin/health/dapr/history?from={ISO8601}&to={ISO8601}&component={name}` returns `DaprComponentHealthTimeline` containing all entries matching the time range and optional component filter. Defaults: `from` = 24 hours ago, `to` = now, `component` = null (all components). Maximum queryable range: 7 days (returns 400 Bad Request if exceeded). Maximum entries returned: 50,000 (`AdminServerOptions.MaxHealthHistoryEntriesPerQuery`, default: 50,000). If the result set exceeds this cap, entries are truncated to the most recent 50,000 and the response includes `IsTruncated = true` so the UI can display a warning banner ("Results truncated — narrow the time range or filter by component for complete data"). Requires `ReadOnly` authorization policy. If no history data exists for the range, returns an empty timeline with `HasData = false`.

4. **AC4: Timeline heatmap visualization** — The page displays a CSS-based heatmap grid showing component health over time. Rows = component names (sorted alphabetically). Columns = time slots (1-hour granularity for 24h view, 15-minute granularity for 6h view, 5-minute granularity for 1h view). Each cell is a colored `<div>` using the established status color tokens: green (`--hexalith-status-success`) = Healthy, yellow (`--hexalith-status-warning`) = Degraded, red (`--hexalith-status-error`) = Unhealthy, gray (`--hexalith-status-neutral`) = No data. Cell color represents the **worst status** observed in that time slot. Hovering a cell shows a native HTML `title` attribute tooltip with: component name, time slot range, and status (e.g., "statestore | 14:00-15:00 | Healthy: 8, Degraded: 2"). Use `title` instead of `<FluentTooltip>` to avoid rendering hundreds of tooltip components in dense heatmaps (28 columns x N components). Clicking a cell filters the transition log (AC5) to that component and time range. The heatmap uses CSS Grid (`display: grid`) with no external charting library dependency. Add `role="grid"` and `aria-label="DAPR component health history heatmap"` to the container, and `role="row"` / `role="gridcell"` to rows and cells for screen reader accessibility.

5. **AC5: Status transition log** — Below the heatmap, a `FluentDataGrid<DaprHealthHistoryEntry>` shows individual status transitions (only entries where status changed from the previous entry for that component). Columns: Timestamp (formatted as `HH:mm:ss` for same-day, `MMM dd HH:mm` for older), Component Name, Previous Status (as `StatusBadge`), New Status (as `StatusBadge`), Duration in previous status (human-readable, e.g., "2h 15m"). A `FluentSearch` input filters by component name. Sorted by timestamp descending (most recent first). When filtered via heatmap cell click, a `FluentBadge` shows the active filter with a clear button.

6. **AC6: Per-component drill-down** — Clicking a component name (in heatmap row header or transition log) opens a detail panel showing: (a) Current status as large `StatusBadge`, (b) Uptime percentage for the selected time range (calculated as healthy-minutes / total-minutes * 100, displayed as `StatCard` with green >= 99.9%, yellow >= 99%, red < 99%), (c) Status distribution bar (horizontal stacked bar using CSS `linear-gradient` showing proportion of Healthy/Degraded/Unhealthy time), (d) A `FluentButton` "View Component" navigating to `/dapr?type={componentCategory}` (deep link to story 19-1 component detail). The detail panel appears as a `FluentCard` below the heatmap, replacing any previous selection.

7. **AC7: Time-range picker** — A row of `FluentButton` toggles with `Appearance.Outline` for preset ranges: "1 Hour", "6 Hours", "24 Hours" (default), "3 Days", "7 Days". The selected button uses `Appearance.Accent`. Changing the time range triggers a fresh API call and re-renders the heatmap and transition log. A custom range is NOT needed for v1 (presets cover the key investigation scenarios).

8. **AC8: Summary stat cards** — A summary row at the top with four `StatCard` components: Total Components (count, neutral), Currently Healthy (count, success severity), Status Changes (count in selected time range, neutral), Uptime % (average across all components for selected range — green >= 99.9%, yellow >= 99%, red < 99%). When history is unavailable, all show "N/A".

9. **AC9: Page routing and navigation** — Route is `/dapr/health-history`. The DAPR components page (`/dapr`, from story 19-1) includes a `FluentButton` with `Appearance.Outline` labeled "Health History" near the sidecar status card section (adjacent to the "Actor Inspector", "Pub/Sub Metrics", and "Resiliency Policies" buttons from stories 19-2, 19-3, and 19-4). The NavMenu does NOT add a separate link. The `DaprHealthHistory.razor` page includes a `FluentAnchor` back-link to `/dapr`.

10. **AC10: Loading, empty, and error states** — `SkeletonCard` shows during initial load. No auto-refresh timer on this page (history data is retrospective — users control the time range explicitly). Manual "Refresh" `FluentButton` triggers a fresh data fetch. If health history collection is disabled: `EmptyState` with title "Health history collection is disabled" and description "Set `AdminServer:HealthHistoryEnabled` to `true` in appsettings to enable background health snapshot collection." If collection is enabled but no data exists yet: `EmptyState` with title "No health history available yet" and description "Health snapshots are collected every 60 seconds. Data will appear after the first collection cycle." If state store is unreachable: `IssueBanner` with "Unable to retrieve health history — state store unavailable."

11. **AC11: Deep linking** — The page supports URL query parameters: `?component={name}` to pre-select a component drill-down on load, `?range={1h|6h|24h|3d|7d}` to set the initial time range (default: `24h`). Use `[SupplyParameterFromQuery]` for both parameters.

## Tasks / Subtasks

- [x] Task 1: Create new models in Admin.Abstractions (AC: #1, #2, #3, #4, #5, #6, #8)
  - [x] 1.1 Create `DaprHealthHistoryEntry` record in `Models/Dapr/DaprHealthHistoryEntry.cs`
  - [x] 1.2 Create `DaprComponentHealthTimeline` record in `Models/Dapr/DaprComponentHealthTimeline.cs`
  - [x] 1.3 Add `HealthHistoryEnabled`, `HealthHistoryCaptureIntervalSeconds`, `HealthHistoryRetentionDays`, `MaxHealthHistoryEntriesPerQuery` properties to `AdminServerOptions`
- [x] Task 2: Create background health snapshot collector (AC: #1, #2)
  - [x] 2.1 Create `DaprHealthHistoryCollector` as `BackgroundService` in Admin.Server `Services/DaprHealthHistoryCollector.cs`
  - [x] 2.2 Register `DaprHealthHistoryCollector` as hosted service in `AddAdminServer()` method
- [x] Task 3: Extend health query service (AC: #3)
  - [x] 3.1 Add `GetComponentHealthHistoryAsync(DateTimeOffset from, DateTimeOffset to, string? componentName, CancellationToken ct)` to `IHealthQueryService`
  - [x] 3.2 Implement `GetComponentHealthHistoryAsync` in `DaprHealthQueryService`
- [x] Task 4: Add REST endpoint to existing controller (AC: #3)
  - [x] 4.1 Add `GetComponentHealthHistoryAsync` endpoint to `AdminHealthController`
- [x] Task 5: Create UI API client (AC: #3)
  - [x] 5.1 Create `AdminHealthHistoryApiClient` in Admin.UI `Services/AdminHealthHistoryApiClient.cs`
  - [x] 5.2 Register `AdminHealthHistoryApiClient` as scoped in `Program.cs` (after existing API client registrations)
- [x] Task 6: Create health history page (AC: #4, #5, #6, #7, #8, #9, #10, #11)
  - [x] 6.1 Create `DaprHealthHistory.razor` page in Admin.UI `Pages/`
  - [x] 6.2 Add "Health History" button to `DaprComponents.razor` (from story 19-1)
- [x] Task 7: Write tests (all ACs)
  - [x] 7.1 Model tests in Admin.Abstractions.Tests (`Models/Dapr/`)
  - [x] 7.2 Background collector tests in Admin.Server.Tests (`Services/`)
  - [x] 7.3 Service history query tests in Admin.Server.Tests (`Services/`)
  - [x] 7.4 Controller tests in Admin.Server.Tests (`Controllers/`)
  - [x] 7.5 UI page tests in Admin.UI.Tests (`Pages/`)

### Review Findings

- [x] [Review][Patch] `from > to` inverted time range not validated in controller — FIXED: added `from > to` validation returning 400 Bad Request [AdminHealthController.cs]
- [x] [Review][Patch] `DaprHealthHistoryEntry` constructor validation throws on deserialization — FIXED: replaced throwing validation with null-coalescing to `string.Empty` for deserialization safety [DaprHealthHistoryEntry.cs]
- [x] [Review][Patch] API client swallows errors and returns `null` → UI incorrectly shows "disabled" — FIXED: API client now re-throws non-auth errors; only returns `null` for `NotImplemented` (disabled) [AdminHealthHistoryApiClient.cs]
- [x] [Review][Patch] `from`/`to` query parameters not URL-encoded in API client — FIXED: wrapped with `Uri.EscapeDataString` [AdminHealthHistoryApiClient.cs]
- [x] [Review][Patch] `ComputeTransitions` dictionary uses case-sensitive default comparer — FIXED: uses `StringComparer.OrdinalIgnoreCase` [DaprHealthHistory.razor]
- [x] [Review][Patch] UI page does not pass `component` query parameter to API call — FIXED: passes `_selectedComponent` to `GetHealthHistoryAsync` [DaprHealthHistory.razor]
- [x] [Review][Patch] Heatmap tooltip shows single time point instead of time range — FIXED: shows `Start–End` range format [DaprHealthHistory.razor]
- [x] [Review][Patch] `MaxHealthHistoryEntriesPerQuery` has no validation — FIXED: added `> 0` check in `AdminServerOptionsValidator` [AdminServerOptionsValidator.cs]
- [x] [Review][Defer] Unbounded state store growth per day partition — no cap on entries per day key; with low intervals and many components the day-partition JSON can grow large — deferred, accepted design in spec
- [x] [Review][Defer] `DaprComponentHealthTimeline.HasData` flag is redundant with `Entries.Count > 0` — creates a consistency invariant the type cannot enforce; `with` expressions allow inconsistent state — deferred, pre-existing design pattern
- [x] [Review][Defer] `IHealthQueryService.GetComponentHealthHistoryAsync` has no range guard at service level — controller limits to 7 days but a future direct caller could pass multi-year ranges generating hundreds of parallel state store reads — deferred, no non-controller callers exist yet
- [x] [Review][Defer] Missing interactive UI tests vs spec expectations — spec lists "time range buttons trigger data reload", "component click opens drill-down", "deep link parameters applied on load" as expected tests; not implemented — deferred, test enhancement

## Dev Notes

### Architecture Compliance

This story follows the **exact same architecture** as stories 19-1 through 19-4 and all Epic 14/15/19 patterns, with one addition: a `BackgroundService` for periodic data capture.

- **Models:** Immutable C# `record` types with constructor validation (`ArgumentException` / `ArgumentNullException`). Located in `Admin.Abstractions/Models/Dapr/` (same subfolder as all other DAPR models).
- **Service extension:** Extends the existing `IHealthQueryService` and `DaprHealthQueryService` — do NOT create a separate service interface for history queries. Add the new method to the existing interface and implementation. **IMPORTANT:** Adding a method to `IHealthQueryService` requires awareness that NSubstitute mocks in existing health test files will inherit the new method. NSubstitute stubs return default values for unconfigured methods, so existing tests should still compile and pass — but verify.
- **Controller extension:** Extends the existing `AdminHealthController` — do NOT create a separate controller. Add new action method to the existing controller (route: `dapr/history` under the existing `api/v1/admin/health` base).
- **Background service:** New `DaprHealthHistoryCollector : BackgroundService` in `Admin.Server/Services/`. Uses `IServiceScopeFactory` to create scoped service instances per tick (since `DaprClient` and query services are scoped). Registered via `services.AddHostedService<DaprHealthHistoryCollector>()` in `AddAdminServer()`.
- **UI API client:** New `AdminHealthHistoryApiClient` in `Admin.UI/Services/`. Uses `IHttpClientFactory` with `"AdminApi"` named client. Virtual async methods for testability. `HandleErrorStatus(response)` pattern from `AdminDaprApiClient`.
- **Page:** New `DaprHealthHistory.razor` at `/dapr/health-history`. Implements `IDisposable` (NOT `IAsyncDisposable` — no PeriodicTimer needed since history is retrospective, user-driven). Injects `AdminHealthHistoryApiClient` and `NavigationManager`.
- **No auto-refresh:** Unlike stories 19-1/19-2/19-3, health history is retrospective data that users explore with explicit time-range selection. Therefore: NO `PeriodicTimer`, NO `IAsyncDisposable`, NO `CancellationTokenSource`. Use `OnInitializedAsync` load with manual "Refresh" button and time-range picker triggers.

### Key Data Source: DAPR State Store

**Health snapshots are persisted to the DAPR state store** — the same state store already configured via `AdminServerOptions.StateStoreName` (default: `"statestore"`).

#### State Store Key Design

```
Key format: admin:health-history:{yyyyMMdd}   (UTC date)
Example:    admin:health-history:20260327

Value: JSON-serialized DaprComponentHealthTimeline containing all entries for that day
```

**Why day-partitioned keys:**
- **Efficient range queries:** A 7-day query reads at most 7 keys (one `GetStateAsync` per day)
- **Simple retention cleanup:** Delete keys older than retention period by date calculation
- **Bounded value size:** At 60-second intervals with ~5 components, one day = ~7,200 entries (~200 KB JSON) — well within state store value limits
- **No need for a separate database or time-series store** — DAPR state store is sufficient for this admin-level telemetry

#### Write Pattern (Background Collector)

```csharp
// In DaprHealthHistoryCollector.ExecuteAsync():
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    if (!_options.HealthHistoryEnabled)
    {
        _logger.LogInformation("Health history collection is disabled");
        return;
    }

    // Allow DAPR sidecar to initialize before first probe
    await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ConfigureAwait(false);

    // Run retention cleanup on startup
    await CleanupExpiredHistoryAsync(stoppingToken).ConfigureAwait(false);

    // Capture first snapshot immediately (don't wait for first timer tick)
    await CaptureSnapshotAsync(stoppingToken).ConfigureAwait(false);

    using PeriodicTimer timer = new(TimeSpan.FromSeconds(_options.HealthHistoryCaptureIntervalSeconds));
    DateTimeOffset lastCleanup = DateTimeOffset.UtcNow;

    while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
    {
        try
        {
            await CaptureSnapshotAsync(stoppingToken).ConfigureAwait(false);

            // Daily cleanup check
            if (DateTimeOffset.UtcNow.Date > lastCleanup.Date)
            {
                await CleanupExpiredHistoryAsync(stoppingToken).ConfigureAwait(false);
                lastCleanup = DateTimeOffset.UtcNow;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health history capture failed — will retry next cycle");
        }
    }
}

private async Task CaptureSnapshotAsync(CancellationToken ct)
{
    // IMPORTANT: Create a scope per tick — DaprClient and query services are scoped
    using IServiceScope scope = _scopeFactory.CreateScope();
    IDaprInfrastructureQueryService infraService = scope.ServiceProvider
        .GetRequiredService<IDaprInfrastructureQueryService>();
    DaprClient daprClient = scope.ServiceProvider.GetRequiredService<DaprClient>();

    IReadOnlyList<DaprComponentDetail> components = await infraService
        .GetComponentsAsync(ct)
        .ConfigureAwait(false);

    // Skip write if no components returned (sidecar unreachable) — avoids empty snapshots
    if (components.Count == 0)
    {
        _logger.LogDebug("No DAPR components returned — skipping health history snapshot");
        return;
    }

    DateTimeOffset now = DateTimeOffset.UtcNow;
    string dayKey = $"admin:health-history:{now:yyyyMMdd}";

    // Read current day's timeline (may be null if first entry today)
    DaprComponentHealthTimeline? existing = await daprClient
        .GetStateAsync<DaprComponentHealthTimeline>(_options.StateStoreName, dayKey, cancellationToken: ct)
        .ConfigureAwait(false);

    List<DaprHealthHistoryEntry> entries = existing?.Entries?.ToList() ?? [];

    // Append new entries for each component
    foreach (DaprComponentDetail component in components)
    {
        entries.Add(new DaprHealthHistoryEntry(
            ComponentName: component.ComponentName,
            ComponentType: component.ComponentType,
            Status: component.Status,
            CapturedAtUtc: now));
    }

    DaprComponentHealthTimeline updated = new(entries.AsReadOnly(), HasData: true);

    await daprClient
        .SaveStateAsync(_options.StateStoreName, dayKey, updated, cancellationToken: ct)
        .ConfigureAwait(false);
}
```

**CRITICAL: Scoped service resolution.** The `BackgroundService` is a singleton, but `DaprClient`, `IDaprInfrastructureQueryService`, and `DaprHealthQueryService` are registered as scoped. The collector MUST use `IServiceScopeFactory` to create a new scope per tick. Injecting scoped services directly into a singleton constructor will throw at startup. Follow the standard ASP.NET Core pattern: inject `IServiceScopeFactory` + `IOptions<AdminServerOptions>` + `ILogger<T>` in the constructor.

#### Read Pattern (Query Service)

```csharp
// In DaprHealthQueryService.GetComponentHealthHistoryAsync():
public async Task<DaprComponentHealthTimeline> GetComponentHealthHistoryAsync(
    DateTimeOffset from,
    DateTimeOffset to,
    string? componentName,
    CancellationToken ct)
{
    // Calculate which day keys to query
    List<string> dayKeys = [];
    for (DateTimeOffset date = from.Date; date.Date <= to.Date; date = date.AddDays(1))
    {
        dayKeys.Add($"admin:health-history:{date:yyyyMMdd}");
    }

    // Read all day partitions in parallel
    Task<DaprComponentHealthTimeline?>[] tasks = dayKeys
        .Select(key => _daprClient.GetStateAsync<DaprComponentHealthTimeline>(
            _options.StateStoreName, key, cancellationToken: ct))
        .ToArray();

    DaprComponentHealthTimeline?[] results = await Task.WhenAll(tasks).ConfigureAwait(false);

    // Merge and filter
    List<DaprHealthHistoryEntry> allEntries = results
        .Where(t => t?.Entries is not null)
        .SelectMany(t => t!.Entries)
        .Where(e => e.CapturedAtUtc >= from && e.CapturedAtUtc <= to)
        .Where(e => componentName is null
            || e.ComponentName.Equals(componentName, StringComparison.OrdinalIgnoreCase))
        .OrderBy(e => e.CapturedAtUtc)
        .ToList();

    // Apply entry cap to prevent excessive memory/payload on large queries
    bool isTruncated = allEntries.Count > _options.MaxHealthHistoryEntriesPerQuery;
    if (isTruncated)
    {
        allEntries = allEntries
            .OrderByDescending(e => e.CapturedAtUtc)
            .Take(_options.MaxHealthHistoryEntriesPerQuery)
            .OrderBy(e => e.CapturedAtUtc)
            .ToList();
    }

    return new DaprComponentHealthTimeline(allEntries.AsReadOnly(), HasData: allEntries.Count > 0, IsTruncated: isTruncated);
}
```

#### Retention Cleanup

```csharp
private async Task CleanupExpiredHistoryAsync(CancellationToken ct)
{
    int consecutiveFailures = 0;
    // Delete keys for dates before cutoff, going back up to 30 days max to avoid unbounded cleanup
    for (int i = _options.HealthHistoryRetentionDays + 1; i <= _options.HealthHistoryRetentionDays + 30; i++)
    {
        DateTimeOffset date = DateTimeOffset.UtcNow.AddDays(-i);
        string dayKey = $"admin:health-history:{date:yyyyMMdd}";
        try
        {
            await daprClient.DeleteStateAsync(_options.StateStoreName, dayKey, cancellationToken: ct)
                .ConfigureAwait(false);
            consecutiveFailures = 0;
        }
        catch (Exception ex)
        {
            consecutiveFailures++;
            // Escalate to Warning after 3 consecutive failures — may indicate state store issue
            if (consecutiveFailures >= 3)
                _logger.LogWarning(ex, "Repeated cleanup failures ({Count} consecutive) — state store may be unavailable. Key: {Key}", consecutiveFailures, dayKey);
            else
                _logger.LogDebug(ex, "Failed to delete expired health history key: {Key}", dayKey);
        }
    }
}
```

### Model Definitions

#### DaprHealthHistoryEntry

```csharp
// File: Admin.Abstractions/Models/Dapr/DaprHealthHistoryEntry.cs
namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// A single health observation for a DAPR component at a specific point in time.
/// </summary>
public record DaprHealthHistoryEntry(
    string ComponentName,
    string ComponentType,
    HealthStatus Status,
    DateTimeOffset CapturedAtUtc)
{
    public string ComponentName { get; } = !string.IsNullOrWhiteSpace(ComponentName)
        ? ComponentName
        : throw new ArgumentException("Component name is required.", nameof(ComponentName));

    public string ComponentType { get; } = !string.IsNullOrWhiteSpace(ComponentType)
        ? ComponentType
        : throw new ArgumentException("Component type is required.", nameof(ComponentType));
}
```

#### DaprComponentHealthTimeline

```csharp
// File: Admin.Abstractions/Models/Dapr/DaprComponentHealthTimeline.cs
namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Aggregated health history for DAPR components over a time range.
/// </summary>
public record DaprComponentHealthTimeline(
    IReadOnlyList<DaprHealthHistoryEntry> Entries,
    bool HasData,
    bool IsTruncated = false)
{
    /// <summary>
    /// Creates an empty timeline indicating no data is available.
    /// </summary>
    public static DaprComponentHealthTimeline Empty { get; } = new([], false);
}
```

### AdminServerOptions Extensions

Add these properties to the existing `AdminServerOptions` class:

```csharp
/// <summary>
/// Whether background health history collection is enabled. Default: true.
/// </summary>
public bool HealthHistoryEnabled { get; set; } = true;

/// <summary>
/// Interval in seconds between health snapshot captures. Default: 60 (1 minute).
/// Minimum: 10. Values below 10 are clamped to 10 to prevent excessive state store writes.
/// </summary>
public int HealthHistoryCaptureIntervalSeconds { get; set; } = 60;

/// <summary>
/// Number of days to retain health history in state store. Default: 7.
/// Older entries are cleaned up daily. Minimum: 1. Maximum: 30.
/// </summary>
public int HealthHistoryRetentionDays { get; set; } = 7;

/// <summary>
/// Maximum number of history entries returned per query. Default: 50,000.
/// Prevents excessive memory usage on large time-range queries with many components.
/// Results exceeding this cap are truncated to the most recent entries.
/// </summary>
public int MaxHealthHistoryEntriesPerQuery { get; set; } = 50_000;
```

### REST Endpoint Design

```csharp
// In AdminHealthController:
/// <summary>
/// Gets DAPR component health history for a time range.
/// </summary>
[HttpGet("dapr/history")]
[ProducesResponseType(typeof(DaprComponentHealthTimeline), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> GetComponentHealthHistoryAsync(
    [FromQuery] DateTimeOffset? from = null,
    [FromQuery] DateTimeOffset? to = null,
    [FromQuery] string? component = null,
    CancellationToken ct = default)
{
    DateTimeOffset effectiveFrom = from ?? DateTimeOffset.UtcNow.AddHours(-24);
    DateTimeOffset effectiveTo = to ?? DateTimeOffset.UtcNow;

    if ((effectiveTo - effectiveFrom).TotalDays > 7)
    {
        return BadRequest(new ProblemDetails
        {
            Title = "Time range too large",
            Detail = "Maximum queryable range is 7 days.",
            Status = StatusCodes.Status400BadRequest
        });
    }

    DaprComponentHealthTimeline timeline = await _healthQueryService
        .GetComponentHealthHistoryAsync(effectiveFrom, effectiveTo, component, ct)
        .ConfigureAwait(false);

    return Ok(timeline);
}
```

### UI API Client Pattern

```csharp
// File: Admin.UI/Services/AdminHealthHistoryApiClient.cs
public class AdminHealthHistoryApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<AdminHealthHistoryApiClient> logger)
{
    public virtual async Task<DaprComponentHealthTimeline?> GetHealthHistoryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? component = null,
        CancellationToken ct = default)
    {
        try
        {
            HttpClient client = httpClientFactory.CreateClient("AdminApi");
            string url = $"api/v1/admin/health/dapr/history?from={from:O}&to={to:O}";
            if (!string.IsNullOrEmpty(component))
                url += $"&component={Uri.EscapeDataString(component)}";

            HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            HandleErrorStatus(response);
            return await response.Content
                .ReadFromJsonAsync<DaprComponentHealthTimeline>(cancellationToken: ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to fetch health history");
            return null;
        }
    }

    // Copy HandleErrorStatus from AdminDaprApiClient — same pattern
}
```

### CSS-Based Timeline Heatmap (No External Charting Library)

**CRITICAL: Do NOT add a charting library.** The heatmap is built with CSS Grid, which is sufficient for this grid-of-colored-cells visualization and avoids adding a new NuGet dependency.

```html
<!-- Heatmap structure in DaprHealthHistory.razor -->
<div class="health-heatmap"
     role="grid"
     aria-label="DAPR component health history heatmap"
     style="
    display: grid;
    grid-template-columns: 180px repeat(@_timeSlotCount, 1fr);
    gap: 1px;
    background: var(--neutral-stroke-rest);
    border: 1px solid var(--neutral-stroke-rest);
    border-radius: 4px;
    overflow: hidden;">

    <!-- Header row: time labels -->
    <div class="heatmap-header"></div>
    @foreach (var slot in _timeSlots)
    {
        <div class="heatmap-header" style="font-size: 10px; text-align: center; padding: 4px 0;">
            @slot.Label
        </div>
    }

    <!-- Component rows -->
    @foreach (var component in _componentNames)
    {
        <div class="heatmap-row-label" style="padding: 4px 8px; cursor: pointer;"
             @onclick="() => SelectComponent(component)">
            @component
        </div>
        @foreach (var slot in _timeSlots)
        {
            @{
                var cellStatus = GetWorstStatus(component, slot);
                var cellColor = StatusToColor(cellStatus);
            }
            <div class="heatmap-cell"
                 style="background: @cellColor; min-height: 24px; cursor: pointer;"
                 title="@component | @slot.Label | @GetStatusSummary(component, slot)"
                 @onclick="() => FilterByCell(component, slot)">
            </div>
        }
    }
</div>
```

**Time slot calculation:**

| Range   | Granularity | Slots |
|---------|-------------|-------|
| 1 hour  | 5 minutes   | 12    |
| 6 hours | 15 minutes  | 24    |
| 24 hours| 1 hour      | 24    |
| 3 days  | 3 hours     | 24    |
| 7 days  | 6 hours     | 28    |

The slot count is kept at 24-28 to ensure the heatmap remains readable without horizontal scrolling at standard viewport widths.

**`GetWorstStatus` logic:** For a given component and time slot, filter entries whose `CapturedAtUtc` falls within the slot, then return the worst status: Unhealthy > Degraded > Healthy > NoData.

**`StatusToColor` mapping:**
- `HealthStatus.Healthy` → `var(--hexalith-status-success)` (green)
- `HealthStatus.Degraded` → `var(--hexalith-status-warning)` (yellow)
- `HealthStatus.Unhealthy` → `var(--hexalith-status-error)` (red)
- No data → `var(--hexalith-status-neutral)` (gray)

### Transition Log Computation

The transition log shows only **status changes**, not every snapshot. Compute on the client side:

```csharp
private List<StatusTransition> ComputeTransitions(DaprComponentHealthTimeline timeline)
{
    var transitions = new List<StatusTransition>();
    var lastStatusByComponent = new Dictionary<string, (HealthStatus Status, DateTimeOffset Time)>();

    foreach (var entry in timeline.Entries.OrderBy(e => e.CapturedAtUtc))
    {
        if (lastStatusByComponent.TryGetValue(entry.ComponentName, out var last))
        {
            if (last.Status != entry.Status)
            {
                transitions.Add(new StatusTransition(
                    entry.CapturedAtUtc,
                    entry.ComponentName,
                    PreviousStatus: last.Status,
                    NewStatus: entry.Status,
                    DurationInPreviousStatus: entry.CapturedAtUtc - last.Time));

                lastStatusByComponent[entry.ComponentName] = (entry.Status, entry.CapturedAtUtc);
            }
        }
        else
        {
            lastStatusByComponent[entry.ComponentName] = (entry.Status, entry.CapturedAtUtc);
        }
    }

    return transitions.OrderByDescending(t => t.Timestamp).ToList();
}

private record StatusTransition(
    DateTimeOffset Timestamp,
    string ComponentName,
    HealthStatus PreviousStatus,
    HealthStatus NewStatus,
    TimeSpan DurationInPreviousStatus);
```

### Page Lifecycle (Simplified — No PeriodicTimer)

```csharp
@page "/dapr/health-history"
@implements IDisposable

// Parameters
[SupplyParameterFromQuery] public string? Component { get; set; }
[SupplyParameterFromQuery] public string? Range { get; set; }

// State
private string _selectedRange = "24h";
private DaprComponentHealthTimeline? _timeline;
private string? _selectedComponent;
private bool _isLoading = true;
private string? _errorMessage;

protected override async Task OnInitializedAsync()
{
    if (!string.IsNullOrEmpty(Range))
        _selectedRange = Range;
    if (!string.IsNullOrEmpty(Component))
        _selectedComponent = Component;

    await LoadDataAsync();
}

private async Task LoadDataAsync()
{
    _isLoading = true;
    _errorMessage = null;
    StateHasChanged();

    try
    {
        (DateTimeOffset from, DateTimeOffset to) = GetTimeRange(_selectedRange);
        _timeline = await _historyClient.GetHealthHistoryAsync(from, to, ct: default);
    }
    catch (Exception ex)
    {
        _errorMessage = ex.Message;
    }
    finally
    {
        _isLoading = false;
        StateHasChanged();
    }
}

public void Dispose() { /* No resources to dispose — no timer */ }
```

### Future Enhancements (Out of Scope for v1)

- **Transition-only storage:** Instead of storing every snapshot, store only status transitions plus periodic heartbeats (e.g., every 15 minutes). Reduces storage by ~90% and makes the transition log a direct read. Trade-off: heatmap rendering requires forward-filling status between transitions. Worth considering if component count grows beyond ~15.
- **Response caching:** Add 30-second server-side cache on the history endpoint. Health history is append-only — the same query returns the same result within a capture cycle. Reduces CPU on repeated queries.
- **Component category filter on heatmap:** Add a `DaprComponentCategory` dropdown filter (reuse story 19-1's pattern) to the heatmap for scenarios with many components (>15). Show only State Store, PubSub, etc.
- **ETag-based concurrency:** For multi-instance deployments, use DAPR's ETag-based optimistic concurrency on `SaveStateAsync` with a single retry on conflict. See AC2 multi-instance note.

### Previous Story Patterns to Reuse

- **`DaprComponents.razor` navigation buttons:** Follow the exact button placement pattern — `FluentButton` with `Appearance.Outline` and `Icon` adjacent to existing "Actor Inspector", "Pub/Sub Metrics", and "Resiliency Policies" buttons.
- **`AdminDaprApiClient` error handling:** Copy `HandleErrorStatus()` method for `AdminHealthHistoryApiClient`.
- **Model validation:** Follow `DaprComponentDetail` constructor validation pattern for new records.
- **Test patterns:** Follow `DaprComponentDetailTests` for model tests, `DaprHealthQueryServiceTests` for service tests, `AdminDaprControllerTests` for controller tests.

### Git Intelligence

Recent commits show stories 19-1, 19-2, 19-3 all followed the same pattern:
1. Models in Abstractions
2. Service interface extension
3. Controller extension
4. UI client creation
5. Razor page creation
6. Tests across all layers

Story 19-4 (resiliency) added a unique element — YAML file reading. Story 19-5 similarly adds a unique element — `BackgroundService` for periodic state store writes. This is the first `BackgroundService` in the Admin.Server project.

### Testing Strategy

**Model tests (Admin.Abstractions.Tests):**
- `DaprHealthHistoryEntryTests`: Valid construction, empty name throws, empty type throws
- `DaprComponentHealthTimelineTests`: Empty entries allowed, `Empty` static property works, `HasData` flag correct

**Background collector tests (Admin.Server.Tests):**
- `DaprHealthHistoryCollectorTests`:
  - Disabled option → collector exits immediately without state store writes
  - Enabled → captures snapshot and writes to state store with correct key format
  - Empty component list → skips state store write, logs at Debug level
  - State store write failure → logs warning, continues to next cycle
  - Cleanup deletes keys older than retention period
  - Uses `IServiceScopeFactory` mock to provide scoped services

**Service query tests (Admin.Server.Tests):**
- `DaprHealthQueryServiceHealthHistoryTests`:
  - Returns merged entries from multiple day partitions
  - Filters by component name (case-insensitive)
  - Filters by time range
  - Returns empty timeline when no data exists
  - Truncates results when exceeding `MaxHealthHistoryEntriesPerQuery`, sets `IsTruncated = true`
  - Rejects time range > 7 days (handled at controller level, but service should not throw)

**Controller tests (Admin.Server.Tests):**
- `AdminHealthControllerHistoryTests`:
  - Returns 200 with timeline data
  - Returns 400 for range exceeding 7 days
  - Boundary: exactly 7 days (168 hours) returns 200; 7 days + 1 second returns 400
  - Default parameters (no from/to) default to last 24 hours
  - Component filter passes through correctly

**UI page tests (Admin.UI.Tests):**
- `DaprHealthHistoryPageTests`:
  - Renders heatmap grid when data available
  - Renders EmptyState when no data
  - Renders truncation warning banner when `IsTruncated = true`
  - Time range buttons trigger data reload
  - Component click opens drill-down panel
  - Deep link parameters applied on load

### Project Structure Notes

All new files align with established project structure:

```
src/Hexalith.EventStore.Admin.Abstractions/
  Models/Dapr/DaprHealthHistoryEntry.cs         (NEW)
  Models/Dapr/DaprComponentHealthTimeline.cs     (NEW)

src/Hexalith.EventStore.Admin.Server/
  Services/DaprHealthHistoryCollector.cs          (NEW)
  Services/DaprHealthQueryService.cs              (EXTEND)
  Configuration/AdminServerOptions.cs             (EXTEND)
  Configuration/ServiceCollectionExtensions.cs    (EXTEND - register hosted service)
  Controllers/AdminHealthController.cs            (EXTEND)

src/Hexalith.EventStore.Admin.UI/
  Services/AdminHealthHistoryApiClient.cs         (NEW)
  Pages/DaprHealthHistory.razor                   (NEW)
  Pages/DaprComponents.razor                      (EXTEND - add button)
  Program.cs                                      (EXTEND - register API client)

tests/Hexalith.EventStore.Admin.Abstractions.Tests/
  Models/Dapr/DaprHealthHistoryEntryTests.cs      (NEW)
  Models/Dapr/DaprComponentHealthTimelineTests.cs (NEW)

tests/Hexalith.EventStore.Admin.Server.Tests/
  Services/DaprHealthHistoryCollectorTests.cs     (NEW)
  Services/DaprHealthQueryServiceHistoryTests.cs  (NEW)
  Controllers/AdminHealthControllerHistoryTests.cs (NEW)

tests/Hexalith.EventStore.Admin.UI.Tests/
  Pages/DaprHealthHistoryPageTests.cs             (NEW)
```

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 19] — Epic scope and story list
- [Source: _bmad-output/planning-artifacts/prd.md#FR75] — Operational health dashboard requirement
- [Source: _bmad-output/planning-artifacts/architecture.md#ADR-P5] — Deep-link strategy (no embedded observability)
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#D5] — Health-first monitor UX direction
- [Source: _bmad-output/implementation-artifacts/19-1-dapr-component-status-dashboard.md] — Foundation patterns
- [Source: _bmad-output/implementation-artifacts/19-3-dapr-pubsub-delivery-metrics.md] — Service extension pattern
- [Source: _bmad-output/implementation-artifacts/19-4-dapr-resiliency-policy-viewer.md] — Static page pattern (no auto-refresh)
- [Source: src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs] — Health probe implementation
- [Source: src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs] — State store read/write patterns
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Health/] — Existing health models

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- All 1,822 Tier 1 tests pass (0 failures, 0 regressions)
- Full solution builds with 0 warnings on relevant projects (2 pre-existing errors in Admin.Cli and IntegrationTests are unrelated)

### Completion Notes List

- Created `DaprHealthHistoryEntry` and `DaprComponentHealthTimeline` immutable records with constructor validation following existing model patterns
- Extended `AdminServerOptions` with 4 health history configuration properties (enabled, interval, retention, max entries)
- Created `DaprHealthHistoryCollector` as first `BackgroundService` in Admin.Server — uses `IServiceScopeFactory` for scoped DI, 15s initial delay, periodic 60s capture, day-partitioned state store keys, daily retention cleanup
- Registered collector as hosted service in `AddAdminServer()` DI method
- Extended `IHealthQueryService` with `GetComponentHealthHistoryAsync` — parallel day-partition reads, time-range and component filtering, entry truncation with `IsTruncated` flag
- Added `GET api/v1/admin/health/dapr/history` endpoint with 7-day max range validation, optional component filter, default 24h range
- Created `AdminHealthHistoryApiClient` with same `HandleErrorStatus` pattern as existing API clients
- Created `DaprHealthHistory.razor` page with: CSS Grid heatmap (5 time granularities), status transition log with `FluentDataGrid`, per-component drill-down panel with uptime % and status distribution bar, time-range picker (5 presets), 4 summary stat cards, deep linking via `[SupplyParameterFromQuery]`, loading/empty/error/truncation states
- Added "Health History" button to `DaprComponents.razor` navigation bar
- Wrote 30+ new tests across 6 test files (model validation, collector behavior, service queries, controller endpoints, UI page rendering)

### Change Log

- 2026-03-27: Implemented story 19-5 DAPR Component Health History — all tasks complete, all tests pass

### File List

**New files:**
- src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprHealthHistoryEntry.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Dapr/DaprComponentHealthTimeline.cs
- src/Hexalith.EventStore.Admin.Server/Services/DaprHealthHistoryCollector.cs
- src/Hexalith.EventStore.Admin.UI/Services/AdminHealthHistoryApiClient.cs
- src/Hexalith.EventStore.Admin.UI/Pages/DaprHealthHistory.razor
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Dapr/DaprHealthHistoryEntryTests.cs
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Dapr/DaprComponentHealthTimelineTests.cs
- tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprHealthHistoryCollectorTests.cs
- tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprHealthQueryServiceHistoryTests.cs
- tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminHealthControllerHistoryTests.cs
- tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DaprHealthHistoryPageTests.cs

**Modified files:**
- src/Hexalith.EventStore.Admin.Server/Configuration/AdminServerOptions.cs
- src/Hexalith.EventStore.Admin.Server/Configuration/ServiceCollectionExtensions.cs
- src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs
- src/Hexalith.EventStore.Admin.Server/Controllers/AdminHealthController.cs
- src/Hexalith.EventStore.Admin.Abstractions/Services/IHealthQueryService.cs
- src/Hexalith.EventStore.Admin.UI/Pages/DaprComponents.razor
- src/Hexalith.EventStore.Admin.UI/Program.cs
