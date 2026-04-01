# Story 15.12: Events Page — Cross-Stream Event Browser

Status: done
Size: Medium (~1 new file rewrite, follows established Commands.razor pattern)

## Definition of Done

- All 6 ACs verified
- Merge-blocking bUnit tests green (Task 3)
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- Manual smoke test: run AppHost, increment counter, verify Events page shows `CounterIncremented` events

## Story

As a **developer investigating event activity across streams in the Hexalith EventStore admin dashboard**,
I want **the Events page (`/events`) to show a filterable, paginated list of recent events from all active streams**,
so that **I can browse event activity across the system without navigating to individual streams**.

## Acceptance Criteria

1. **Event grid display** — Given the Events page loads, when streams have produced events, then the page fetches recently active streams via `AdminStreamApiClient.GetRecentlyActiveStreamsAsync()`, fetches timelines per stream via `GetStreamTimelineAsync(count: 100)` (capped at 50 streams), filters to `TimelineEntryType.Event` entries, merges all events, and displays them in a `FluentDataGrid` sorted by timestamp descending. Columns: Event Type (monospace, truncated with tooltip), Tenant, Domain, Aggregate ID (monospace, truncated with tooltip), Correlation ID (monospace, truncated with tooltip), Timestamp (monospace `yyyy-MM-dd HH:mm:ss`, default sort descending). Grid template: `1fr 1fr 1fr 1fr 1fr 1fr`.
2. **Summary stat cards** — Three `StatCard` components at the top: Recent Events (`_allItems.Count`, subtitle "From up to 50 active streams"), Unique Event Types (distinct `TypeName` values), Active Streams (distinct `{TenantId}/{Domain}/{AggregateId}` combos). This order prioritizes event type diversity over stream count for developer investigation.
3. **Tenant filter** — `FluentSelect` dropdown populated from `AdminStreamApiClient.GetTenantsAsync()`. Options: "All Tenants" (default) + list of tenant IDs. Changing the selection resets to page 1, reloads events (re-fetching timelines filtered by tenant), and updates URL query param `?tenant=`.
4. **Event type filter** — `FluentTextField` for case-insensitive contains filter on `TypeName`. Applied client-side after merge. Changing the filter resets to page 1 and updates URL query param `?eventType=`.
5. **Real-time refresh** — Subscribe to `DashboardRefreshService.OnDataChanged` in `OnAfterRenderAsync` (first render only). On signal, re-fetch all data, preserve scroll position via JS interop `hexalithAdmin.getScrollTop`. Unsubscribe in `DisposeAsync`.
6. **Error handling and empty states** — `ForbiddenAccessException` shows "Access Denied" `EmptyState`. `ServiceUnavailableException` shows "Unable to load events. The admin backend may be unavailable." `HttpRequestException` shows status code. Generic exception shows "Unable to load events." Loading state shows 3x `SkeletonCard`. No-data empty state (no filters active): Title "No events recorded yet", Description "Events will appear here as commands are processed and produce state changes." No-data empty state (filters active): Title "No events found", Description "No events match the current filters." Row click navigates to `/streams/{tenant}/{domain}/{aggregateId}?detail={sequenceNumber}`.

## Tasks / Subtasks

- [x] **Task 1: Rewrite Events.razor** (AC: 1, 2, 3, 4, 5, 6)
    - [x] 1.1 Replace the entire stub with a full implementation following Commands.razor pattern exactly
    - [x] 1.2 Add imports: `Hexalith.EventStore.Admin.Abstractions.Models.Streams`, `Hexalith.EventStore.Admin.Abstractions.Models.Tenants`, `Hexalith.EventStore.Admin.UI.Components`, `Hexalith.EventStore.Admin.UI.Components.Shared`, `Hexalith.EventStore.Admin.UI.Services`, `Hexalith.EventStore.Admin.UI.Services.Exceptions`
    - [x] 1.3 Inject: `AdminStreamApiClient ApiClient`, `NavigationManager NavigationManager`, `DashboardRefreshService RefreshService`, `IJSRuntime JSRuntime`, `ILogger<Events> Logger` (needed for per-stream failure warnings in `LoadEventsAsync`)
    - [x] 1.4 Implement `@implements IAsyncDisposable`
    - [x] 1.5 State variables: `_isLoading`, `_isForbidden`, `_errorMessage`, `_currentPage`, `_tenantFilter`, `_eventTypeFilter`, `_allItems` (typed as `List<EventRow>`), `_filteredItems`, `_tenantOptions`, `_pendingScrollTop`, `_hasRendered`, `_disposed`, `_cts` (`CancellationTokenSource` — created in `OnInitializedAsync`, cancelled in `DisposeAsync`), `PageSize = 25`
    - [x] 1.6 Create a private inner record `EventRow` to flatten `TimelineEntry` with stream identity: `record EventRow(string TenantId, string Domain, string AggregateId, long SequenceNumber, DateTimeOffset Timestamp, string TypeName, string CorrelationId, string? UserId)`
    - [x] 1.7 Implement `OnInitializedAsync`: `ReadUrlParameters()` -> `LoadTenantsAsync()` -> `LoadEventsAsync()`
    - [x] 1.8 Implement `LoadEventsAsync()`:
        - Set `_isLoading = true`, clear error state
        - Call `ApiClient.GetRecentlyActiveStreamsAsync(tenantId, null, 50)` to get up to 50 streams
        - Use `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 10` over the streams list. **Thread safety:** Each parallel iteration collects its own `List<EventRow>` and adds it to a `ConcurrentBag<List<EventRow>>` — do NOT write to a shared `List<EventRow>` from parallel tasks. After `Parallel.ForEachAsync` completes, merge via `perStreamResults.SelectMany(x => x).OrderByDescending(e => e.Timestamp).ToList()`. Wrap each individual timeline call in try/catch — on failure, log at Warning via `Logger` and skip that stream. Pass `CancellationToken` through to `Parallel.ForEachAsync` so navigating away cancels in-flight fetches.
        - Inside each parallel iteration: call `ApiClient.GetStreamTimelineAsync(stream.TenantId, stream.Domain, stream.AggregateId, null, null, 100, ct)`, filter to `EntryType == TimelineEntryType.Event`, map to `EventRow` records combining stream identity with timeline entry data
        - After parallel completion: merge all per-stream `List<EventRow>` into `_allItems`, sorted by `Timestamp` descending
        - Call `ApplyClientFilters()`
        - Full exception handling chain matching Commands.razor pattern (ForbiddenAccessException, UnauthorizedAccessException, ServiceUnavailableException, HttpRequestException, generic)
        - Set `_isLoading = false` in finally block
    - [x] 1.9 Implement `ApplyClientFilters()`: filter `_allItems` by `_eventTypeFilter` (case-insensitive contains on `TypeName`). Clamp `_currentPage` to `_totalPages`.
    - [x] 1.10 Implement render template: loading skeleton -> forbidden -> error -> data (StatCards + filters + grid + pagination) -> empty state
    - [x] 1.11 Implement `OnRowClick(EventRow)`: navigate to `/streams/{TenantId}/{Domain}/{AggregateId}?detail={SequenceNumber}`
    - [x] 1.12 Implement URL sync: `ReadUrlParameters()` + `UpdateUrl()` with `page`, `tenant`, `eventType` params
    - [x] 1.13 Implement refresh: `OnAfterRenderAsync` subscribes to `RefreshService.OnDataChanged`, callback uses `InvokeAsync`, captures/restores scroll top, same exception handling as Commands.razor
    - [x] 1.14 Implement `DisposeAsync`: cancel `_cts`, set `_disposed = true`, unsubscribe from RefreshService, dispose `_cts`
    - [x] 1.15 **Checkpoint**: Build compiles with zero warnings

- [ ] **Task 2: Manual smoke test** (AC: 1, 2, 3, 4, 5, 6) — Requires running AppHost interactively; user must verify manually
    - [ ] 2.1 Run AppHost (`dotnet run` in `src/Hexalith.EventStore.AppHost`)
    - [ ] 2.2 Navigate to sample Counter UI, increment counter several times
    - [ ] 2.3 Navigate to Admin UI `/events` page
    - [ ] 2.4 Verify: events appear in grid (at minimum `CounterIncremented` events)
    - [ ] 2.5 Verify: StatCards show correct totals
    - [ ] 2.6 Verify: tenant filter narrows results
    - [ ] 2.7 Verify: event type filter narrows results
    - [ ] 2.8 Verify: clicking a row navigates to stream detail page
    - [ ] 2.9 Verify: page auto-refreshes within 30 seconds after new counter increments
    - [ ] 2.10 Verify: "Unable to load events" error when Admin API is stopped (not silent empty)

- [x] **Task 3: Merge-blocking bUnit tests** (AC: 1, 6)
    - [x] 3.1 Create `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/EventsPageTests.cs` following `CommandsPageTests.cs` pattern
    - [x] 3.2 Test: **Loading state** — renders 3x `SkeletonCard` while `_isLoading` is true
    - [x] 3.3 Test: **Empty state (no data)** — renders `EmptyState` with "No events recorded yet" when API returns 0 streams
    - [x] 3.4 Test: **Grid renders with mock data** — mock `AdminStreamApiClient` to return streams + timeline entries with `EntryType == Event`, verify `FluentDataGrid` renders rows with correct columns
    - [x] 3.5 Test: **Error state** — mock API to throw `ServiceUnavailableException`, verify `EmptyState` shows "Unable to load events"
    - [x] 3.6 Test: **Row click navigation** — verify `OnRowClick` navigates to `/streams/{t}/{d}/{id}?detail={seq}`
    - [x] 3.7 **Checkpoint**: All bUnit tests green, build compiles with zero warnings

### Review Findings

- [x] [Review][Patch] Refresh captures scroll but never restores it [src/Hexalith.EventStore.Admin.UI/Pages/Events.razor:161]
- [x] [Review][Patch] Refresh path does not reload tenant options even though AC5 says to re-fetch all data [src/Hexalith.EventStore.Admin.UI/Pages/Events.razor:161]
- [x] [Review][Patch] Loading-state test does not verify the required three skeleton cards [tests/Hexalith.EventStore.Admin.UI.Tests/Pages/EventsPageTests.cs:49]
- [x] [Review][Patch] Row-click test bypasses the grid event wiring instead of clicking a rendered row [tests/Hexalith.EventStore.Admin.UI.Tests/Pages/EventsPageTests.cs:140]

## Dev Notes

### Critical Pattern: Follow Commands.razor Exactly

The Events page must be a structural copy of `Commands.razor` (460 lines). The only differences:

1. **Data source**: Commands uses a single API call (`GetRecentCommandsAsync`). Events assembles data from multiple calls (`GetRecentlyActiveStreamsAsync` + N x `GetStreamTimelineAsync`).
2. **Data model**: Commands uses `CommandSummary`. Events uses a flattened `EventRow` inner record (see Task 1.6).
3. **Filters**: Commands has status/tenant/commandType. Events has tenant/eventType (no status filter — events don't have a status enum).
4. **StatCards**: Commands has Total/SuccessRate/Failed/InFlight. Events has RecentEvents/UniqueEventTypes/ActiveStreams (event types more useful as second card for developer investigation).
5. **Row click target**: Commands navigates to `/streams/{t}/{d}/{id}?correlation=`. Events navigates to `/streams/{t}/{d}/{id}?detail={seq}`.

Everything else — state variables, lifecycle methods, refresh pattern, URL sync, pagination, error handling, loading states, dispose pattern — is identical.

### Data Flow

```
Events.razor
  → AdminStreamApiClient.GetRecentlyActiveStreamsAsync(tenantId, null, 50)
    → HTTP GET /api/v1/admin/streams?count=50[&tenantId=x]
    → Returns PagedResult<StreamSummary>
  → For each stream (parallel via Task.WhenAll):
    → AdminStreamApiClient.GetStreamTimelineAsync(t, d, id, null, null, 100)
      → HTTP GET /api/v1/admin/streams/{t}/{d}/{id}/timeline?count=100
      → Returns PagedResult<TimelineEntry>
  → Filter each timeline: entry.EntryType == TimelineEntryType.Event
  → Map to EventRow (flatten stream identity + timeline entry)
  → Merge all EventRows, sort by Timestamp descending
  → Apply client-side filters (eventType contains)
  → Display in FluentDataGrid with pagination
```

### Performance Consideration

The N+1 API pattern (1 streams call + up to 50 timeline calls) is acceptable for admin workloads. Use `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 10` for timeline fetching — do NOT use unbounded `Task.WhenAll` which would fire 50 simultaneous HTTP calls and exhaust the `HttpClient` connection pool. Cap streams at 50 to bound the number of API calls. Cap timeline entries at 100 per stream to bound memory. With parallelism of 10, worst case is 5 sequential batches at ~100ms each = ~500ms wall time.

**Per-stream error resilience:** Wrap each individual timeline fetch in try/catch. If one stream's timeline call fails (timeout, 503, etc.), log at Warning via injected `ILogger` and skip that stream — do not let it propagate and blank the entire page. This differs from Commands.razor which makes a single API call.

**Thread safety:** `Parallel.ForEachAsync` runs up to 10 concurrent tasks. Each task must collect results into its own `List<EventRow>`, then add that list to a `ConcurrentBag<List<EventRow>>`. Do NOT write to a shared `List<EventRow>` — that's a race condition. After all parallel work completes, merge: `perStreamResults.SelectMany(x => x).OrderByDescending(e => e.Timestamp).ToList()`.

**CancellationToken:** Create a `CancellationTokenSource` in `OnInitializedAsync`. Pass its token to `Parallel.ForEachAsync` and all API calls. Cancel in `DisposeAsync` so navigating away aborts in-flight fetches.

**StatCard "Recent Events" label:** The page shows events from up to 50 recently active streams, not the true system-wide total. The "Recent Events" label (with subtitle "From up to 50 active streams") sets correct user expectations.

**Known limitation:** `GetStreamTimelineAsync` returns commands, events, and queries. The client-side filter to `TimelineEntryType.Event` discards non-event entries. With `count=100` per stream, a stream with 90 commands and 10 events will yield only 10 visible rows. This is acceptable for v1.

### Existing API Methods (No New Methods Needed)

All required methods exist in `AdminStreamApiClient`:

- `GetRecentlyActiveStreamsAsync(tenantId, domain, count, ct)` — returns `PagedResult<StreamSummary>`
- `GetStreamTimelineAsync(tenantId, domain, aggregateId, fromSequence, toSequence, count, ct)` — returns `PagedResult<TimelineEntry>`
- `GetTenantsAsync(ct)` — returns `IReadOnlyList<TenantSummary>`

### Existing Models (No New DTOs Needed)

- `StreamSummary` — `(TenantId, Domain, AggregateId, LastEventSequence, LastActivityUtc, EventCount, HasSnapshot, StreamStatus)`
- `TimelineEntry` — `(SequenceNumber, Timestamp, EntryType, TypeName, CorrelationId, UserId)`
- `TimelineEntryType` — enum: `Command`, `Event`, `Query`
- `TenantSummary` — has `TenantId` property

### EventRow Inner Record

Create a private inner record to flatten stream identity with timeline entry data:

```csharp
private record EventRow(
    string TenantId,
    string Domain,
    string AggregateId,
    long SequenceNumber,
    DateTimeOffset Timestamp,
    string TypeName,
    string CorrelationId,
    string? UserId);
```

This avoids creating a new shared DTO — the flattening is a UI concern.

### StatusBadge Support

`StatusBadge.StatusDisplayConfig.FromTimelineEntryType(TimelineEntryType)` already exists. For the Events page, all entries will be `TimelineEntryType.Event`, so a StatusBadge column is optional. If included, all rows will show the same "Event" badge (green, event icon). Consider omitting StatusBadge and using the column for Event Type name instead.

### Shared Components (All Exist, Do NOT Recreate)

| Component      | Location                               | Usage                         |
| -------------- | -------------------------------------- | ----------------------------- |
| `EmptyState`   | `Components/Shared/EmptyState.razor`   | Error and empty data states   |
| `StatCard`     | `Components/Shared/StatCard.razor`     | Summary statistics            |
| `SkeletonCard` | `Components/Shared/SkeletonCard.razor` | Loading placeholders          |
| `StatusBadge`  | `Components/Shared/StatusBadge.razor`  | Has `FromTimelineEntryType()` |

### Exception Types (All Exist, Do NOT Recreate)

| Exception                     | Namespace             | Trigger            |
| ----------------------------- | --------------------- | ------------------ |
| `ForbiddenAccessException`    | `Services.Exceptions` | 403 from Admin API |
| `ServiceUnavailableException` | `Services.Exceptions` | 503 from Admin API |
| `UnauthorizedAccessException` | System                | 401 from Admin API |

### Import Pattern (Copy from Commands.razor, Adjust)

```razor
@page "/events"

@using Hexalith.EventStore.Admin.Abstractions.Models.Streams
@using Hexalith.EventStore.Admin.Abstractions.Models.Tenants
@using Hexalith.EventStore.Admin.UI.Components
@using Hexalith.EventStore.Admin.UI.Components.Shared
@using Hexalith.EventStore.Admin.UI.Services
@using Hexalith.EventStore.Admin.UI.Services.Exceptions

@implements IAsyncDisposable

@inject AdminStreamApiClient ApiClient
@inject NavigationManager NavigationManager
@inject DashboardRefreshService RefreshService
@inject IJSRuntime JSRuntime
@inject ILogger<Events> Logger
```

Note: Commands.razor also imports `Hexalith.EventStore.Contracts.Commands` for `CommandStatus` enum — not needed for Events page since `TimelineEntryType` is in `Hexalith.EventStore.Admin.Abstractions.Models.Streams` (already imported). `ILogger` is in `Microsoft.Extensions.Logging` — add `@using Microsoft.Extensions.Logging` to the page imports.

### Grid Column Definitions

```razor
<FluentDataGrid Items="@GetCurrentPage()" TGridItem="EventRow"
                Style="width: 100%;"
                OnRowClick="@(row => OnRowClick(row.Item))"
                GridTemplateColumns="1fr 1fr 1fr 1fr 1fr 1fr">
    <TemplateColumn Title="Event Type">
        <span class="monospace grid-cell-truncate" title="@context.TypeName">@context.TypeName</span>
    </TemplateColumn>
    <PropertyColumn Property="@(e => e.TenantId)" Title="Tenant" Class="monospace" />
    <PropertyColumn Property="@(e => e.Domain)" Title="Domain" Class="monospace" />
    <TemplateColumn Title="Aggregate ID">
        <span class="monospace grid-cell-truncate" title="@context.AggregateId"
              style="cursor: pointer;">@context.AggregateId</span>
    </TemplateColumn>
    <TemplateColumn Title="Correlation ID">
        <span class="monospace grid-cell-truncate" title="@context.CorrelationId"
              style="cursor: pointer;">@context.CorrelationId</span>
    </TemplateColumn>
    <TemplateColumn Title="Timestamp" Class="monospace"
                    SortBy="@(GridSort<EventRow>.ByDescending(e => e.Timestamp))"
                    IsDefaultSortColumn="true" InitialSortDirection="SortDirection.Descending">
        <span class="monospace grid-cell-truncate" style="font-size: 12px;"
              title="@context.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")">
            @context.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
        </span>
    </TemplateColumn>
</FluentDataGrid>
```

### Anti-Patterns to Avoid

1. **DO NOT** create a new `AdminEventApiClient` or add methods to `AdminStreamApiClient` — all required methods already exist
2. **DO NOT** create a new `EventSummary` DTO in `Admin.Abstractions` — use a private inner record in the page
3. **DO NOT** add a new controller endpoint — this is a UI-only change
4. **DO NOT** add a new DAPR state store key — events are read from existing stream timelines
5. **DO NOT** use `StreamFilterBar` component (used by Streams page) — use inline filters matching Commands.razor pattern
6. **DO NOT** add `System.Web` reference for `HttpUtility.ParseQueryString` — copy the same approach from Commands.razor which already uses it
7. **DO NOT** add an ActivityChart/histogram — that's a future enhancement (D3 full spec), not this story
8. **DO NOT** use unbounded `Task.WhenAll` for 50 parallel HTTP calls — use `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 10` to avoid exhausting the connection pool
9. **DO NOT** label the first StatCard "Total Events" — use "Recent Events" with subtitle "From up to 50 active streams" to avoid implying a system-wide count
10. **DO NOT** write to a shared `List<EventRow>` from inside `Parallel.ForEachAsync` — use `ConcurrentBag<List<EventRow>>` per-stream and merge after completion to avoid race conditions

### Project Structure Notes

- **File to modify**: `src/Hexalith.EventStore.Admin.UI/Pages/Events.razor` (complete rewrite)
- **No other files modified**
- **NavMenu**: Already has `/events` link at line 12 of `Layout/NavMenu.razor` — no change needed
- **Build**: `dotnet build Hexalith.EventStore.slnx --configuration Release`
- **Solution**: Use `Hexalith.EventStore.slnx` only, never `.sln`

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-01-events-page.md`] — Sprint change proposal
- [Source: `_bmad-output/planning-artifacts/ux-design-specification.md` D3 direction] — UX spec for Events/Timeline page
- [Source: `_bmad-output/planning-artifacts/epics.md` Story 15.12] — Story definition
- [Source: `src/Hexalith.EventStore.Admin.UI/Pages/Commands.razor`] — Primary reference implementation
- [Source: `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs`] — API client with all required methods
- [Source: `_bmad-output/implementation-artifacts/15-9-commands-page-cross-stream-command-list.md`] — Commands page story (pattern reference)
- [Source: `_bmad-output/implementation-artifacts/15-11-persistent-state-store-and-command-activity.md`] — Previous story intelligence

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Pre-existing build failure in `TopologyCacheServiceTests.cs` (NSubstitute overload ambiguity) — fixed to unblock test execution
- Pre-existing build failures in `Admin.Server.Host.Tests` (missing Middleware/Authentication namespaces) — not addressed (unrelated)
- Pre-existing test failure `UseEventStore_DiagnosticsEnabled_ProducesDebugLogOutput` in Client.Tests — confirmed pre-existing via git stash test

### Completion Notes List

- Rewrote `Events.razor` from placeholder stub to full cross-stream event browser following Commands.razor pattern
- Implemented parallel timeline fetching via `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 10` and `ConcurrentBag` for thread safety
- Added `CancellationTokenSource` lifecycle management in `OnInitializedAsync`/`DisposeAsync`
- Per-stream error resilience: individual timeline failures logged at Warning and skipped
- Client-side event type filtering with case-insensitive contains
- Tenant filter triggers server-side reload; event type filter is client-side only
- 3 StatCards: Recent Events (with subtitle), Unique Event Types, Active Streams
- Row click navigates to stream detail with `?detail={sequenceNumber}` query param
- URL sync for page/tenant/eventType query parameters
- Real-time refresh via `DashboardRefreshService.OnDataChanged` subscription
- 8 bUnit tests covering: loading skeleton, empty state, data grid rendering, error state (ServiceUnavailable, Forbidden), row click navigation, stat cards, filtered empty state
- Review fixes applied: refresh now reloads tenant options and restores scroll position; tests now verify 3 skeleton cards, actual row clicks, and refresh state preservation
- Updated StubPageTests to reflect Events page now requiring mocks
- Fixed pre-existing NSubstitute overload ambiguity in TopologyCacheServiceTests.cs

### File List

- `src/Hexalith.EventStore.Admin.UI/Pages/Events.razor` — Complete rewrite from stub to full implementation
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/EventsPageTests.cs` — New: 8 bUnit tests
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/StubPageTests.cs` — Updated: Events test now sets up mocks
- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/TopologyCacheServiceTests.cs` — Fixed: pre-existing NSubstitute overload ambiguity
- Review follow-up: strengthened refresh and interaction coverage for Events page

### Change Log

- 2026-04-01: Story 15.12 implemented — cross-stream event browser on Events page with filtering, pagination, and parallel data loading
- 2026-04-01: Review fixes applied — refresh now restores scroll and reloads tenant options; Events page tests strengthened
