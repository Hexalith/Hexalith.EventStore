# Story 15.9: Commands Page — Cross-Stream Command List with Filters

Status: done
Size: Medium — ~8 new/modified files across 3 layers (Abstractions, Server, UI), 5 task groups, 5 ACs, ~20 tests (~6-10 hours estimated). Reference pattern: Streams.razor page.

## Definition of Done

- All 5 ACs verified
- Merge-blocking bUnit tests green (Task 5 blocking tests)
- Recommended bUnit tests green
- Project builds with zero warnings in CI (`dotnet build --configuration Release`)
- No new analyzer suppressions
- All existing tests (184+ Admin.UI tests) pass — zero regressions

## Story

As a **developer investigating command behavior**,
I want **to see a filterable list of recent commands across all streams**,
so that **I can quickly find and investigate commands without knowing the specific stream**.

## Acceptance Criteria

1. **FluentDataGrid with command columns** — **Given** the Commands page (`/commands`), **When** loaded, **Then** a FluentDataGrid displays recent commands with columns: Status (StatusBadge), Command Type, Tenant, Domain, Aggregate ID (truncated + tooltip, same as Streams), Correlation ID (truncated + tooltip), Timestamp (HH:mm:ss, default sort descending). Empty state shows "No commands found" when no commands match (or "No commands processed yet" when no commands exist at all).

2. **Filter chips** — **Given** the Commands page, **When** filter chips/dropdowns are used, **Then** commands can be filtered by: status (All/Completed/Processing/Rejected/Failed), tenant (dropdown populated from tenant list), and command type (dropdown or text). Filters update URL query params via `NavigationManager.NavigateTo(url, forceLoad: false, replace: true)`. Page loads with filters pre-applied when URL parameters are present.

3. **Summary stat cards** — **Given** the Commands page header, **When** rendered, **Then** summary stat cards show: Total Commands, Success Rate (% Completed), Failed Count (Rejected + PublishFailed + TimedOut), In-Flight Count (Received + Processing + EventsStored + EventsPublished). Cards derive from loaded data (client-side aggregation), not separate API calls.

4. **Row click navigation** — **Given** a command row, **When** clicked, **Then** navigation proceeds to the stream detail page at `/streams/{TenantId}/{Domain}/{AggregateId}?correlation={CorrelationId}`, opening the timeline filtered to that command's correlation ID.

5. **Auto-refresh without state loss** — **Given** the Commands page, **When** the `DashboardRefreshService.OnDataChanged` fires (30s interval), **Then** the command list updates without losing filter selections, pagination position, or scroll state (same pattern as Streams page).

## Tasks / Subtasks

- [x] **Task 1: Create CommandSummary DTO and extend IStreamQueryService** (AC: 1, 2, 3)
    - [x] 1.1 Create `src/Hexalith.EventStore.Admin.Abstractions/Models/Commands/CommandSummary.cs` record:
        ```csharp
        public record CommandSummary(
            string TenantId,
            string Domain,
            string AggregateId,
            string CorrelationId,
            string CommandType,
            CommandStatus Status,
            DateTimeOffset Timestamp,
            int? EventCount,
            string? FailureReason);
        ```
        Follow `StreamSummary` pattern: validate `TenantId`, `Domain`, `AggregateId` non-empty in property getters.
    - [x] 1.2 Add `GetRecentCommandsAsync()` to `IStreamQueryService`:
        ```csharp
        Task<PagedResult<CommandSummary>> GetRecentCommandsAsync(
            string? tenantId,
            string? status,
            string? commandType,
            int count = 1000,
            CancellationToken ct = default);
        ```
    - [x] 1.3 **Checkpoint**: Contracts compile, no warnings.

- [x] **Task 2: Implement DaprStreamQueryService.GetRecentCommandsAsync and controller endpoint** (AC: 1, 2)
    - [x] 2.1 Implement `GetRecentCommandsAsync()` in `DaprStreamQueryService`. Follow the `GetRecentlyActiveStreamsAsync` pattern:
        - Key pattern: `admin:command-activity:{tenantId ?? "all"}` (DAPR state store key)
        - Retrieve `List<CommandSummary>` from state store
        - Apply optional status filter (parse string to `CommandStatus` enum, case-insensitive)
        - Apply optional commandType filter (case-insensitive contains)
        - Order by `Timestamp` descending, take `count`
        - Return `PagedResult<CommandSummary>` with filtered count
        - Catch and log exceptions (return empty result on failure, propagate `OperationCanceledException`)
    - [x] 2.2 Add endpoint to `AdminStreamsController` (keep it in same controller, consistent with streams pattern):
        ```csharp
        [HttpGet("commands")]
        [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
        [ProducesResponseType(typeof(PagedResult<CommandSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetRecentCommands(
            [FromQuery] string? tenantId,
            [FromQuery] string? status,
            [FromQuery] string? commandType,
            [FromQuery] int count = 1000,
            CancellationToken ct = default)
        ```
        Follow exact error handling pattern from `GetRecentlyActiveStreams` (catch `IsServiceUnavailable`, catch non-cancellation). Use `ResolveTenantScope(tenantId)`.
    - [x] 2.3 Route: `GET /api/v1/admin/streams/commands` (nested under existing streams controller route prefix).
    - [x] 2.4 **Checkpoint**: API endpoint returns 200 with empty result when no data, build clean.

- [x] **Task 3: Add client method to AdminStreamApiClient** (AC: 1, 2)
    - [x] 3.1 Add `GetRecentCommandsAsync()` to `AdminStreamApiClient`:
        ```csharp
        public virtual async Task<PagedResult<CommandSummary>> GetRecentCommandsAsync(
            string? tenantId,
            string? status,
            string? commandType,
            int count = 1000,
            CancellationToken ct = default)
        ```
        Follow existing pattern: build URL with query params, use `httpClientFactory.CreateClient("AdminApi")`, call `HandleErrorStatus(response)`, deserialize `PagedResult<CommandSummary>`, return `_emptyCommandsResult` on null.
    - [x] 3.2 Add static empty result: `private static readonly PagedResult<CommandSummary> _emptyCommandsResult = new([], 0, null);`
    - [x] 3.3 Add URL builder: `BuildCommandsUrl(tenantId, status, commandType, count)` following `BuildStreamsUrl` pattern. Endpoint: `api/v1/admin/streams/commands`.
    - [x] 3.4 Exception handling: same pattern as streams — catch non-critical exceptions, log, return empty. Propagate `UnauthorizedAccessException`, `ForbiddenAccessException`, `ServiceUnavailableException`, `OperationCanceledException`.
    - [x] 3.5 **Checkpoint**: Client compiles, no warnings.

- [x] **Task 4: Implement Commands.razor page** (AC: 1, 2, 3, 4, 5)
    - [x] 4.1 Replace `Commands.razor` stub with full implementation. **Inject**: `AdminStreamApiClient`, `NavigationManager`, `DashboardRefreshService`, `IJSRuntime`. Implement `IAsyncDisposable`.
    - [x] 4.2 **State variables** (follow Streams.razor pattern):
        - `_isLoading`, `_errorMessage`, `_isForbidden`
        - `_currentPage` (int, default 1), `PageSize = 25` (const)
        - `_statusFilter` (string?, nullable — "All" means null)
        - `_tenantFilter` (string?, nullable)
        - `_commandTypeFilter` (string?, nullable)
        - `_allItems` (IReadOnlyList\<CommandSummary\>), `_filteredItems` (IReadOnlyList\<CommandSummary\>)
        - `_tenantOptions` (List\<string\>) for tenant dropdown
    - [x] 4.3 **OnInitializedAsync flow** (same as Streams):
        1. `ReadUrlParameters()` — parse query params: `page`, `status`, `tenant`, `commandType`
        2. `await LoadTenantsAsync()` — call `AdminStreamApiClient.GetTenantsAsync()` to populate dropdown
        3. `await LoadCommandsAsync()` — call `GetRecentCommandsAsync(tenantFilter, statusFilter, commandTypeFilter)`
        4. Subscribe: `RefreshService.OnDataChanged += OnRefreshSignal;`
    - [x] 4.4 **Filter bar rendering** — Inline filter bar (no separate component needed, simpler than StreamFilterBar which is also inline in Streams.razor):
        - Status dropdown: All / Completed / Processing / Rejected / Failed (map "Failed" to include Rejected, PublishFailed, TimedOut for UX simplicity, or keep 4 explicit values)
        - Tenant dropdown: populated from `_tenantOptions`
        - Command type text input (optional)
        - Each filter change calls `ApplyFilters()` → `UpdateUrl()` → `StateHasChanged()`
    - [x] 4.5 **Summary stat cards** — 4 cards above the grid:
        - Total Commands: `_allItems.Count` (before client-side filtering but after server filtering)
        - Success Rate: `Completed / Total * 100` (show as percentage)
        - Failed: count where Status is Rejected, PublishFailed, or TimedOut
        - In-Flight: count where Status is Received, Processing, EventsStored, or EventsPublished
        - Use same card styling pattern as Index page stat cards
    - [x] 4.6 **FluentDataGrid columns** — 7 columns:
        - Status: `TemplateColumn` → `StatusBadge` with command status config (see Task 4.8)
        - Command Type: `PropertyColumn` → `context.CommandType`, monospace
        - Tenant: `PropertyColumn` → `context.TenantId`, monospace
        - Domain: `PropertyColumn` → `context.Domain`, monospace
        - Aggregate ID: `TemplateColumn` → truncate to 8 chars + ellipsis with full ID in title (same as Streams)
        - Correlation ID: `TemplateColumn` → truncate to 8 chars + ellipsis with full ID in title
        - Timestamp: `TemplateColumn` → `context.Timestamp.ToString("HH:mm:ss")`, default sort descending (`GridSort<CommandSummary>.ByDescending(c => c.Timestamp)`, `IsDefaultSortColumn=true`)
    - [x] 4.7 **Row click** → `NavigationManager.NavigateTo($"/streams/{Uri.EscapeDataString(context.TenantId)}/{Uri.EscapeDataString(context.Domain)}/{Uri.EscapeDataString(context.AggregateId)}?correlation={Uri.EscapeDataString(context.CorrelationId)}")`. Use `OnRowClick` callback same as Streams.
    - [x] 4.8 **StatusBadge for CommandStatus** — Add `FromCommandStatus` factory to `StatusBadge.StatusDisplayConfig`:
        ```csharp
        public static StatusDisplayConfig FromCommandStatus(CommandStatus status) => status switch
        {
            CommandStatus.Completed => new("Completed", "\u25CF", "var(--hexalith-status-success)"),
            CommandStatus.Received => new("Received", "\u25CB", "var(--hexalith-status-neutral)"),
            CommandStatus.Processing => new("Processing", "\u25CB", "var(--hexalith-status-inflight)"),
            CommandStatus.EventsStored => new("Events Stored", "\u25CB", "var(--hexalith-status-inflight)"),
            CommandStatus.EventsPublished => new("Events Published", "\u25CB", "var(--hexalith-status-inflight)"),
            CommandStatus.Rejected => new("Rejected", "\u2612", "var(--hexalith-status-error)"),
            CommandStatus.PublishFailed => new("Publish Failed", "\u2612", "var(--hexalith-status-error)"),
            CommandStatus.TimedOut => new("Timed Out", "\u2612", "var(--hexalith-status-warning)"),
            _ => new(status.ToString(), "\u2022", "var(--hexalith-status-neutral)"),
        };
        ```
    - [x] 4.9 **Pagination** — Same pattern as Streams: `PageSize = 25`, Previous/Next buttons, "Page X of Y" + "N total" display. `GetCurrentPage()` with LINQ Skip/Take.
    - [x] 4.10 **Auto-refresh** — `OnRefreshSignal` callback: `_ = InvokeAsync(async () => { await LoadCommandsAsync(); StateHasChanged(); });` with try/catch for `ObjectDisposedException`. Unsubscribe in `DisposeAsync()`.
    - [x] 4.11 **URL synchronization** — `UpdateUrl()` method: build query string from `_currentPage`, `_statusFilter`, `_tenantFilter`, `_commandTypeFilter`. Use `NavigationManager.NavigateTo(url, forceLoad: false, replace: true)`.
    - [x] 4.12 **Loading skeleton** — Show 3 `SkeletonCard` components while `_isLoading` is true (same as Streams).
    - [x] 4.13 **Error/forbidden state** — Catch `ForbiddenAccessException` separately, show "Access Denied" empty state. Other errors show generic error message.
    - [x] 4.14 **Checkpoint**: Commands page renders grid, filters work, navigation works, auto-refresh works.

- [x] **Task 5: Unit tests (bUnit)** (AC: 1-5)
    - **Test class**: `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/CommandsPageTests.cs`
    - **Setup**: Extend `AdminUITestContext`. Mock `AdminStreamApiClient` with NSubstitute. Register `DashboardRefreshService`, `TestSignalRClient`. Follow `StreamsPageTests` pattern exactly.
    - **Merge-blocking tests**:
    - [x] 5.1 Test grid renders command data (Status, CommandType, TenantId, Domain, AggregateId, CorrelationId, Timestamp) — feed mock with 3 commands, verify all fields appear in markup (AC: 1)
    - [x] 5.2 Test empty state shows "No commands processed yet" when API returns empty list (AC: 1)
    - [x] 5.3 Test StatusBadge renders correct label for Completed, Rejected, Processing statuses (AC: 1)
    - [x] 5.4 Test row click navigates to stream detail with correlation ID query param (AC: 4)
    - [x] 5.5 Test pagination shows "Page 1 of 2" and "30 total" for 30 items with PageSize 25 (AC: 1)
    - [x] 5.6 Test aggregate ID truncation: 16-char ID shows as 8 chars + ellipsis with full ID in title (AC: 1)
    - [x] 5.7 Test correlation ID truncation: same pattern as aggregate ID (AC: 1)
    - **Recommended tests**:
    - [x] 5.8 Test summary stat cards compute correct values: total, success rate, failed count, in-flight count (AC: 3)
    - [x] 5.9 Test forbidden access shows "Access Denied" empty state (AC: 1)
    - [x] 5.10 Test loading skeleton appears while data is loading (AC: 1)
    - [x] 5.11 Test `FromCommandStatus` mapping covers all 8 CommandStatus enum values (AC: 1)
    - [x] 5.12 Test filter application updates URL query parameters (AC: 2)
    - [x] 5.13 Test page loads with URL filters pre-applied (AC: 2)
    - [x] 5.14 **Checkpoint**: All tests pass, zero warnings.

    ### Review Findings
    - [x] [Review][Patch] End-to-end status and command-type filtering is incomplete; the page fetches only the first 1000 unfiltered commands and applies those filters client-side, while the backend status filter only exact-parses enum values instead of supporting the story's composite `Processing` and `Failed` mappings [src/Hexalith.EventStore.Admin.UI/Pages/Commands.razor:225; src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs:76]
    - [x] [Review][Patch] Command-load failures are hidden as empty results, making the page's generic error state effectively unreachable and backend command-index read failures indistinguishable from "no commands" [src/Hexalith.EventStore.Admin.UI/Pages/Commands.razor:219,239; src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs:100]
    - [x] [Review][Patch] The filtered empty state can incorrectly say "No commands processed yet" for a tenant-scoped empty result even when other tenants have commands, because the page decides between global-empty and filtered-empty using only the already filtered `_allItems.Count` [src/Hexalith.EventStore.Admin.UI/Pages/Commands.razor:120,225]
    - [x] [Review][Patch] Auto-refresh does not preserve all required user state: scroll position is never captured/restored despite `IJSRuntime` injection, and invalid post-refresh pagination falls back to page 1 instead of the nearest valid page [src/Hexalith.EventStore.Admin.UI/Pages/Commands.razor:16,152,277]
    - [x] [Review][Patch] The Commands grid header is labeled `Time` instead of the story's required `Timestamp`, and the current tests codify that mismatch [src/Hexalith.EventStore.Admin.UI/Pages/Commands.razor:95; tests/Hexalith.EventStore.Admin.UI.Tests/Pages/CommandsPageTests.cs:50]
    - [x] [Review][Patch] The new tests do not meaningfully verify AC2, AC4, or AC5: the row-click test never triggers navigation, the URL/filter tests only assert placeholder conditions, and there is no refresh-state coverage [tests/Hexalith.EventStore.Admin.UI.Tests/Pages/CommandsPageTests.cs:99,114,262,275,279,291]

## Dev Notes

### Architecture Compliance

- **UX-D1 (Command-Centric)**: Commands page at `/commands` with FluentDataGrid, filter chips, sortable columns, summary stat cards. This is Jerome's primary entry point for command investigation (Journey 2).
- **ADR-P4**: Admin.UI communicates exclusively via HTTP REST API to Admin.Server. Commands.razor calls `AdminStreamApiClient`, not DAPR directly.
- **SEC-5**: No raw event payload data displayed on this page. Only command metadata (type, status, IDs, timestamps).
- **NFR45**: Supports concurrent users — no shared mutable state. Page state is component-scoped.
- **FR69**: "Unified command/event/query timeline" — this page delivers the cross-stream command list portion.

### Reference Implementation: Streams.razor

The Commands page MUST follow the Streams page pattern precisely. Key patterns:

| Pattern        | Streams.razor Implementation                                                                     | Commands.razor Must Do                                              |
| -------------- | ------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------- |
| Data loading   | `OnInitializedAsync` → `ReadUrlParameters` → `LoadTenantsAsync` → `LoadStreamsAsync` → subscribe | Same flow, `LoadCommandsAsync`                                      |
| API client     | `AdminStreamApiClient.GetRecentlyActiveStreamsAsync()`                                           | `AdminStreamApiClient.GetRecentCommandsAsync()`                     |
| Filter bar     | Inline dropdowns for status, tenant, domain                                                      | Inline dropdowns for status, tenant, commandType                    |
| Pagination     | `PageSize = 25`, Skip/Take, Previous/Next buttons                                                | Identical                                                           |
| URL sync       | `ReadUrlParameters()` + `UpdateUrl()` with `replace: true`                                       | Identical                                                           |
| Auto-refresh   | `RefreshService.OnDataChanged += OnRefreshSignal`                                                | Identical                                                           |
| Row click      | `OnRowClick` → `NavigateTo("/streams/{t}/{d}/{a}")`                                              | `OnRowClick` → `NavigateTo("/streams/{t}/{d}/{a}?correlation={c}")` |
| Error handling | Catch `ForbiddenAccessException` → "Access Denied" empty state                                   | Identical                                                           |
| Dispose        | `IAsyncDisposable`, unsubscribe refresh                                                          | Identical                                                           |
| Loading        | 3 × `SkeletonCard` while `_isLoading`                                                            | Identical                                                           |

### Existing Code to Reuse (DO NOT Recreate)

| What                      | Where                                                                        | How                                                                                                               |
| ------------------------- | ---------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `Commands.razor`          | `src/Hexalith.EventStore.Admin.UI/Pages/Commands.razor`                      | REPLACE stub with full implementation                                                                             |
| `StatusBadge.razor`       | `src/Hexalith.EventStore.Admin.UI/Components/Shared/StatusBadge.razor`       | ADD `FromCommandStatus()` factory to `StatusDisplayConfig`                                                        |
| `EmptyState.razor`        | `src/Hexalith.EventStore.Admin.UI/Components/Shared/EmptyState.razor`        | USE for empty/error/forbidden states                                                                              |
| `SkeletonCard`            | `src/Hexalith.EventStore.Admin.UI/Components/Shared/`                        | USE for loading state (same as Streams)                                                                           |
| `AdminStreamApiClient`    | `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs`          | ADD `GetRecentCommandsAsync()` method                                                                             |
| `IStreamQueryService`     | `src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs` | ADD `GetRecentCommandsAsync()` method                                                                             |
| `DaprStreamQueryService`  | `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs`    | ADD implementation                                                                                                |
| `AdminStreamsController`  | `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` | ADD `GetRecentCommands` endpoint                                                                                  |
| `PagedResult<T>`          | `src/Hexalith.EventStore.Admin.Abstractions/Models/Common/PagedResult.cs`    | USE as return type                                                                                                |
| `CommandStatus` enum      | `src/Hexalith.EventStore.Contracts/Commands/CommandStatus.cs`                | USE — 8 states: Received, Processing, EventsStored, EventsPublished, Completed, Rejected, PublishFailed, TimedOut |
| `DashboardRefreshService` | `src/Hexalith.EventStore.Admin.UI/Services/DashboardRefreshService.cs`       | SUBSCRIBE for 30s auto-refresh                                                                                    |
| `AdminUITestContext`      | `tests/Hexalith.EventStore.Admin.UI.Tests/AdminUITestContext.cs`             | USE as base test class                                                                                            |
| `StreamsPageTests`        | `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/StreamsPageTests.cs`         | REFERENCE for test setup pattern                                                                                  |
| `app.css`                 | `src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css`                       | USE existing stat card and grid styles                                                                            |

### Key Data Flow

```
Commands.razor
  → AdminStreamApiClient.GetRecentCommandsAsync(tenantId, status, commandType)
    → HTTP GET /api/v1/admin/streams/commands?tenantId=X&status=Y&commandType=Z&count=1000
      → AdminStreamsController.GetRecentCommands()
        → IStreamQueryService.GetRecentCommandsAsync()
          → DaprStreamQueryService: DAPR state store key "admin:command-activity:{tenantId}"
```

### CommandStatus to Filter Value Mapping

The AC specifies filter options: All/Completed/Processing/Rejected/Failed. Map these to `CommandStatus` enum values:

| Filter Value | Enum Values Included                                                      |
| ------------ | ------------------------------------------------------------------------- |
| All          | (no filter)                                                               |
| Completed    | `CommandStatus.Completed`                                                 |
| Processing   | `CommandStatus.Received`, `Processing`, `EventsStored`, `EventsPublished` |
| Rejected     | `CommandStatus.Rejected`                                                  |
| Failed       | `CommandStatus.PublishFailed`, `CommandStatus.TimedOut`                   |

The status filter is passed as a string to the API. Server-side filtering maps the string to enum values.

### DAPR State Store Key Pattern

The `DaprStreamQueryService` uses an admin index pattern:

- Streams: `admin:stream-activity:{tenantId ?? "all"}`
- Commands: `admin:command-activity:{tenantId ?? "all"}` (new, same pattern)

This index must be populated by the AggregateActor pipeline when commands are processed. If the index doesn't exist yet, the API returns an empty result (graceful degradation, logged as warning).

### StatusBadge Color Mapping Reference

CSS variables already defined in `app.css`:

- `--hexalith-status-success` (green) → Completed
- `--hexalith-status-inflight` (blue) → Processing, EventsStored, EventsPublished, Received
- `--hexalith-status-error` (red) → Rejected, PublishFailed
- `--hexalith-status-warning` (amber) → TimedOut
- `--hexalith-status-neutral` (gray) → fallback

### Previous Story Intelligence (15-8)

From story 15-8 (Deep Linking and Breadcrumbs):

- Breadcrumb route label dictionary already includes `"commands" → "Commands"` mapping — no update needed
- URL parameter pattern: use `HttpUtility.ParseQueryString(uri.Query)` for reading, `NavigationManager.NavigateTo(url, replace: true)` for writing
- bUnit test pattern: `AdminUITestContext` + NSubstitute for mocking `AdminStreamApiClient` — JSInterop.Mode is Loose
- All 184 Admin.UI tests pass after 15-8 — this is the baseline, zero regressions allowed
- `_disposed` guard pattern for async callbacks is mandatory (prevents `ObjectDisposedException`)

### Git Intelligence

Recent commits show:

- `4132981` — Renamed CommandAPI to EventStore (API route changes)
- `538adb9` — Correlation ID trace map (story 20-5) — adds `GetCorrelationTraceMapAsync` to IStreamQueryService, establishes pattern for cross-stream queries
- `15c2382` — Correlation ID trace map implementation, establishes the `?correlation=` query param pattern on StreamDetail

The correlation trace map work (20-5) is directly relevant: the row click in AC 4 navigates to the stream detail with `?correlation=` parameter, which is already handled by StreamDetail.razor.

### Project Structure Notes

Files to create:

- `src/Hexalith.EventStore.Admin.Abstractions/Models/Commands/CommandSummary.cs` — New DTO
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/CommandsPageTests.cs` — New test file

Files to modify:

- `src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs` — Add `GetRecentCommandsAsync()`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs` — Implement `GetRecentCommandsAsync()`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` — Add `GetRecentCommands` endpoint
- `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs` — Add client method
- `src/Hexalith.EventStore.Admin.UI/Pages/Commands.razor` — Replace stub with full implementation
- `src/Hexalith.EventStore.Admin.UI/Components/Shared/StatusBadge.razor` — Add `FromCommandStatus()` factory

### References

- [Source: epics.md — Story 15.9: Commands Page acceptance criteria]
- [Source: sprint-change-proposal-2026-03-28-commands-page.md — Gap analysis and technical impact]
- [Source: ux-design-specification.md — D1 (Command-Centric), Journey 2 (Jerome's Command Investigation)]
- [Source: ux-design-specification.md — Cross-Page Patterns: status filter chips, summary stat cards]
- [Source: Contracts/Commands/CommandStatus.cs — 8-state enum]
- [Source: Contracts/Commands/CommandStatusRecord.cs — Command status record shape]
- [Source: Admin.UI/Pages/Streams.razor — Reference implementation for grid, filters, pagination, refresh]
- [Source: Admin.UI/Services/AdminStreamApiClient.cs — Client HTTP pattern]
- [Source: Admin.Server/Services/DaprStreamQueryService.cs — DAPR state store query pattern]
- [Source: Admin.Server/Controllers/AdminStreamsController.cs — Controller endpoint pattern]
- [Source: Admin.UI/Components/Shared/StatusBadge.razor — StatusDisplayConfig pattern]
- [Source: Story 15-8 — Breadcrumb already maps "commands" → "Commands", URL parameter patterns]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- bUnit test `fluent-data-grid-row` selector not available for FluentDataGrid `OnRowClick`; row navigation is verified by invoking the page's private click handler and asserting the resolved destination URI.
- `StubPageTests.CommandsPage_RendersCorrectContent` updated to register an explicit `AdminStreamApiClient` mock so the page renders the intended empty state instead of a transport error state.

### Completion Notes List

- Task 1: Created `CommandSummary` record with property validation (follows `StreamSummary` pattern). Added `GetRecentCommandsAsync()` to `IStreamQueryService`. Contracts compile with 0 warnings.
- Task 2: Implemented `GetRecentCommandsAsync()` in `DaprStreamQueryService` with DAPR state store key `admin:command-activity:{tenantId}`, status and commandType filtering. Added `GetRecentCommands` endpoint to `AdminStreamsController` at `GET /api/v1/admin/streams/commands`. Server builds clean.
- Task 3: Added `GetRecentCommandsAsync()` and `BuildCommandsUrl()` to `AdminStreamApiClient` with standard error handling pattern. Client builds clean.
- Task 4: Replaced Commands.razor stub with full implementation: FluentDataGrid with 7 columns, summary stat cards (Total/Success Rate/Failed/In-Flight), inline filter bar (status/tenant/commandType), row click navigation to stream detail with correlation ID, auto-refresh via DashboardRefreshService, URL synchronization, pagination, loading skeleton, and error/forbidden states. Added `FromCommandStatus()` factory to StatusBadge.StatusDisplayConfig.
- Task 5: Created 14 bUnit tests in CommandsPageTests.cs covering all 5 ACs: grid rendering, empty state, status badge mapping, row click navigation, pagination, aggregate/correlation ID truncation, stat card computation, forbidden access, loading skeleton, `FromCommandStatus` enum coverage, filter URL synchronization, URL filter pre-loading, and refresh-state preservation. Updated `StubPageTests` for the new Commands page. All 464 tests pass with 0 regressions.

- Review remediation: closed all 6 review patch findings by moving filter application end-to-end, surfacing load failures as errors, fixing filtered empty-state messaging, preserving scroll/page state on refresh, renaming the grid header to `Timestamp`, and strengthening regression coverage for AC2/AC4/AC5.

### Change Log

- 2026-03-28: Implemented story 15-9 — Commands Page with cross-stream command list, filters, stat cards, and 14 bUnit tests.
- 2026-03-28: Applied code-review remediation for story 15-9 and verified full Admin.Server and Admin.UI test suites.

### File List

**New files:**

- `src/Hexalith.EventStore.Admin.Abstractions/Models/Commands/CommandSummary.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/CommandsPageTests.cs`

**Modified files:**

- `src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/Commands.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/Shared/StatusBadge.razor`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/StubPageTests.cs`
