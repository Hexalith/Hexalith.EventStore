# Story 15.6: Event Type Catalog — Searchable Registry

Status: review
Size: Medium — ~7 new files, 6 task groups, 12 ACs, 17 tests (~10-14 hours estimated)

## Definition of Done

- All 12 ACs verified
- Merge-blocking bUnit tests green (Task 5 blocking tests)
- Recommended bUnit tests green
- Project builds with zero warnings in CI (`dotnet build --configuration Release`)
- No new analyzer suppressions

## Story

As a **developer or DBA using the Hexalith EventStore admin dashboard**,
I want **a searchable type catalog page that lists all registered event types, command types, and aggregate types with their domain, schema version, rejection status, and relationships — filterable by domain and searchable by name**,
so that **I can quickly discover what types exist in the system, understand their relationships (which commands target which aggregates, which events belong to which domains), and investigate type registrations without resorting to code inspection or CLI queries**.

## Acceptance Criteria

1. **Type catalog page** — `/types` page displays three tabbed sections (Events, Commands, Aggregates) using `FluentTabs`. Default tab: Events. Each tab shows a `FluentDataGrid` with type-specific columns. Tab selection persists in URL: `?tab=events|commands|aggregates`. Page title: "Type Catalog". Switching tabs closes the detail panel, clears the current selection, and clears the search text (removes `?search=` from URL). **Exception:** Cross-tab navigation via a TargetAggregateType link (AC 4) is exempt — it switches tab AND selects the matching aggregate without clearing.
2. **Summary stat cards** — Page header shows 3 `StatCard` components: Event Types (count, neutral), Command Types (count, neutral), Aggregate Types (count, neutral). Each card subtitle shows domain breakdown (e.g., "2 domains"). Cards update on data refresh.
3. **Events grid** — `FluentDataGrid<EventTypeInfo>` with columns: Type Name (monospace, sortable), Domain (sortable), Schema Version (numeric, sortable), Rejection (boolean — `FluentBadge` "Rejection" in amber if true, hidden if false). Default sort: Domain ascending, then TypeName ascending.
4. **Commands grid** — `FluentDataGrid<CommandTypeInfo>` with columns: Type Name (monospace, sortable), Domain (sortable), Target Aggregate (monospace, sortable — clickable link that switches to Aggregates tab and highlights matching aggregate). Default sort: Domain ascending, then TypeName ascending.
5. **Aggregates grid** — `FluentDataGrid<AggregateTypeInfo>` with columns: Type Name (monospace, sortable), Domain (sortable), Event Count (numeric, sortable), Command Count (numeric, sortable), Has Projections (boolean — `FluentBadge` "Yes" green if true, "No" neutral if false). Default sort: Domain ascending, then TypeName ascending.
6. **Search filter** — `FluentSearch` text input above the grid, scoped to the active tab. Filters rows where TypeName contains the search string (case-insensitive). Debounce: 300ms. Placeholder: "Search {tab} types...". Persists in URL: `?search=Order`. Clearing the search box restores the full list. Search text is tab-scoped: switching tabs clears `_searchText` and removes `?search=` from the URL (except during cross-tab navigation per AC 1 exception).
7. **Domain filter** — `FluentSelect` dropdown: "All Domains" (default) + distinct domain values from loaded data. Applies across all tabs simultaneously. Persists in URL: `?domain=payments`. Both search and domain filters apply simultaneously (AND logic).
8. **Type detail panel** — Clicking a row in any grid opens a detail panel (responsive: side panel on wide viewport, below grid on narrow) showing type-specific information:
   - **Event detail**: TypeName, Domain, SchemaVersion, IsRejection badge, and a "Related Commands" section listing commands from the same domain (derived client-side from loaded command data).
   - **Command detail**: TypeName, Domain, TargetAggregateType (clickable link to aggregate), and a "Related Events" section listing events from the same domain.
   - **Aggregate detail**: TypeName, Domain, EventCount, CommandCount, HasProjections badge, and two related sections: "Events" listing events from the same domain, and "Commands" listing commands whose TargetAggregateType matches this aggregate's TypeName (precise relationship, not domain-only).
   - Panel header: "{TypeName}" with close button ("Back to List" label).
   - **Related items cap**: If a related section has > 10 items, show first 10 with a "Show all N" `FluentButton` expansion (mirrors the error list pattern from story 15-5, AC 14).
9. **Deep linking** — `/types` (default events tab), `/types?tab=commands&domain=payments` (filtered commands tab), `/types?tab=events&search=Order` (searched events), `/types?tab=events&type=OrderCreated` (detail panel open for specific type). All state encoded as query params on a single `@page "/types"` route. Parse URL params in `OnInitializedAsync` to restore view state. The `?type=` param selects a matching item by TypeName in the active tab's data and opens the detail panel.
10. **Graceful error and empty states** — API failure: `IssueBanner` with "Unable to load type catalog — service may be unavailable" and a "Retry" action button. Empty list: `EmptyState` with "No types registered" (or "No types match the current filters" when filters are active). Individual tab empty: "No {tab} types found".
11. **Accessibility** — Each grid has `aria-label` (e.g., "Event types list"). Search input has `aria-label="Search event types"`. Tab panel has `role="tabpanel"`. Rejection and HasProjections badges use icon + text (not color-only). Tab navigation through grid rows works correctly.
12. **Performance** — All three type lists loaded in a single `Task.WhenAll` call on page init. Grids handle 200+ types without lag. Search filtering is client-side (no API re-call). Domain list derived from loaded data (no separate API call).

## Tasks / Subtasks

- [x] **Task 1: Create AdminTypeCatalogApiClient** (AC: 1, 7, 10, 12)
  - [x] 1.1 Create `Services/AdminTypeCatalogApiClient.cs` with primary constructor: `AdminTypeCatalogApiClient(IHttpClientFactory httpClientFactory, ILogger<AdminTypeCatalogApiClient> logger)`. Use named client `"AdminApi"` (same as `AdminStreamApiClient` and `AdminProjectionApiClient`).
  - [x] 1.2 Add `virtual Task<IReadOnlyList<EventTypeInfo>> ListEventTypesAsync(string? domain, CancellationToken ct = default)` — `GET /api/v1/admin/types/events?domain={domain}`. Return `[]` on failure.
  - [x] 1.3 Add `virtual Task<IReadOnlyList<CommandTypeInfo>> ListCommandTypesAsync(string? domain, CancellationToken ct = default)` — `GET /api/v1/admin/types/commands?domain={domain}`. Return `[]` on failure.
  - [x] 1.4 Add `virtual Task<IReadOnlyList<AggregateTypeInfo>> ListAggregateTypesAsync(string? domain, CancellationToken ct = default)` — `GET /api/v1/admin/types/aggregates?domain={domain}`. Return `[]` on failure.
  - [x] 1.5 Follow existing error handling pattern from `AdminStreamApiClient`: 401 → `UnauthorizedAccessException`, 403 → `ForbiddenAccessException`, 503 → `ServiceUnavailableException`. Log errors with context. Return empty list on non-auth failures.
  - [x] 1.6 All methods use `ConfigureAwait(false)`. URL segments use `Uri.EscapeDataString()`. Domain filter passed as query string param (omit if null).
  - [x] 1.7 Register in `Program.cs`: `builder.Services.AddScoped<AdminTypeCatalogApiClient>();`
  - [x] 1.8 **Checkpoint**: Build compiles with zero warnings.

- [x] **Task 2: Type Catalog page with tabs, stat cards, and grids** (AC: 1, 2, 3, 4, 5, 6, 7, 9, 10, 12)
  - [x] 2.1 Create `Pages/TypeCatalog.razor` — single `@page "/types"`. Inject `AdminTypeCatalogApiClient`, `DashboardRefreshService`, `ViewportService`, `NavigationManager`. Implement `IAsyncDisposable`.
  - [x] 2.2 Page layout: header row with `<h1>Type Catalog</h1>` + refresh button, then stat cards row (3x `StatCard`), then domain filter + search bar row, then `FluentTabs` with 3 tab panels.
  - [x] 2.3 Summary stat cards: Event Types (neutral, subtitle: "{N} domains"), Command Types (neutral, subtitle: "{N} domains"), Aggregate Types (neutral, subtitle: "{N} with projections"). Compute from loaded data.
  - [x] 2.4 `FluentTabs` with `ActiveTabId` and `ActiveTabIdChanged` callback (verify exact FluentUI v4.13.2 Tabs API at build time — may be `@bind-ActiveTabId` or separate property+callback). Tab IDs: "events", "commands", "aggregates". Normal `OnTabChange`: update URL `?tab=`, clear all three selection fields (`_selectedEvent = _selectedCommand = _selectedAggregate = null`), clear `_searchText`, remove `?search=` and `?type=` from URL. **Exception for cross-tab navigation** (called from TargetAggregateType click): switch tab, set `_searchText` to aggregate name, select the matching aggregate — do NOT clear selection.
  - [x] 2.5 **Events tab**: `FluentDataGrid<EventTypeInfo>` with PropertyColumn for TypeName (monospace CSS class), Domain, SchemaVersion. TemplateColumn for Rejection badge (amber `FluentBadge` "Rejection" if `IsRejection`, hidden otherwise). Default sort: Domain then TypeName.
  - [x] 2.6 **Commands tab**: `FluentDataGrid<CommandTypeInfo>` with PropertyColumn for TypeName (monospace), Domain. TemplateColumn for TargetAggregateType as clickable span that switches to Aggregates tab with search pre-filled.
  - [x] 2.7 **Aggregates tab**: `FluentDataGrid<AggregateTypeInfo>` with PropertyColumn for TypeName (monospace), Domain, EventCount, CommandCount. TemplateColumn for HasProjections (`FluentBadge` "Yes" green / "No" neutral).
  - [x] 2.8 `FluentSearch` above the grid. `@bind-Value="_searchText"` with `@oninput` debounced 300ms. Filters current tab's grid items where `TypeName.Contains(search, OrdinalIgnoreCase)`. Placeholder updates per active tab: "Search event types...", "Search command types...", "Search aggregate types...".
  - [x] 2.9 Domain `FluentSelect` dropdown populated from distinct Domain values across all three loaded lists. "All Domains" default. Filters all three lists simultaneously.
  - [x] 2.10 `OnInitializedAsync`: parse URL params (`?tab=`, `?domain=`, `?search=`), load all three type lists via `Task.WhenAll`, subscribe to `DashboardRefreshService.OnDataChanged`.
  - [x] 2.11 Loading state: 3x `SkeletonCard` while `_isLoading`. Error state: `IssueBanner` with retry. Empty state per tab: `EmptyState` with contextual message.
  - [x] 2.12 Row click: set the appropriate typed selection field (`_selectedEvent`, `_selectedCommand`, or `_selectedAggregate` — only one non-null at a time) and update URL with `?type={TypeName}`. Clear the other two selection fields. Single interaction pattern — query param update + inline detail panel. Use a helper `bool HasSelection => _selectedEvent is not null || _selectedCommand is not null || _selectedAggregate is not null;` for the detail panel visibility check.
  - [x] 2.13 Subscribe to `DashboardRefreshService.OnDataChanged` in `OnInitializedAsync`. In handler: re-fetch all type lists, preserve selection by matching TypeName + Domain. Use `InvokeAsync()` wrapper. Handle `ObjectDisposedException`.
  - [x] 2.14 **Checkpoint**: Page loads all three type grids with tabs, search, and domain filter.

- [x] **Task 3: Type detail panel** (AC: 8, 9, 11)
  - [x] 3.1 Create `Components/TypeDetailPanel.razor`:
    - Parameters: `[Parameter] EventTypeInfo? SelectedEvent`, `[Parameter] CommandTypeInfo? SelectedCommand`, `[Parameter] AggregateTypeInfo? SelectedAggregate` (exactly one should be non-null — the parent page enforces this), `[Parameter] IReadOnlyList<EventTypeInfo> AllEvents`, `[Parameter] IReadOnlyList<CommandTypeInfo> AllCommands`, `[Parameter] IReadOnlyList<AggregateTypeInfo> AllAggregates`, `[Parameter] EventCallback OnClose`, `[Parameter] EventCallback<string> OnNavigateToAggregate`.
    - Renders type-specific content based on which parameter is non-null. **Do NOT use `object` as a parameter type** — this triggers Blazor analyzer warning BL0007 which is treated as error.
  - [x] 3.2 **Event detail view**: Header with TypeName, Domain badge, SchemaVersion, IsRejection badge. "Related Commands" section: filter `AllCommands` by same Domain, display as compact list. "Related Aggregates" section: filter `AllAggregates` by same Domain.
  - [x] 3.3 **Command detail view**: Header with TypeName, Domain badge. TargetAggregateType as clickable link (raises `OnNavigateToAggregate`). "Related Events" section: filter `AllEvents` by same Domain. "Related Commands" section: filter `AllCommands` by same Domain and same TargetAggregateType (sibling commands targeting the same aggregate).
  - [x] 3.4 **Aggregate detail view**: Header with TypeName, Domain badge, EventCount, CommandCount, HasProjections badge. "Events" section: filter `AllEvents` by same Domain. "Commands" section: filter `AllCommands` where TargetAggregateType matches this aggregate's TypeName.
  - [x] 3.5 Related type lists render as compact `FluentBadge` chips or simple `<ul>` list with monospace type names. Each is clickable to switch tab and select that type. If > 10 related items, show first 10 with "Show all {N}" `FluentButton` expansion (mirrors 15-5 error list pattern).
  - [x] 3.6 Create `Components/TypeDetailPanel.razor.css` for scoped styles.
  - [x] 3.7 **Checkpoint**: Detail panel opens for each type category and shows relationships.

- [x] **Task 4: Navigation and integration** (AC: 9, 11)
  - [x] 4.1 Add `FluentNavLink` in `NavMenu.razor` after "Projections" and before "Services": `<FluentNavLink Href="/types" Icon="@(new Icons.Regular.Size20.Library())">Types</FluentNavLink>`
  - [x] 4.2 Update `CommandPaletteCatalog.cs`: add entries:
    - `new("Actions", "Type Catalog", "/types")`
    - `new("Types", "Event Types", "/types?tab=events")`
    - `new("Types", "Command Types", "/types?tab=commands")`
    - `new("Types", "Aggregate Types", "/types?tab=aggregates")`
  - [x] 4.3 Verify keyboard navigation: Tab through grid rows, Enter to open detail, Escape to close detail panel.
  - [x] 4.4 **Checkpoint**: Navigation works, command palette resolves correctly.

- [x] **Task 5: Unit tests (bUnit)** (AC: 1-12)
  - **Mock `AdminTypeCatalogApiClient`** — use NSubstitute
  - **Merge-blocking tests** (must pass):
  - [x] 5.1 Test `TypeCatalog` page renders stat cards with correct counts from mock type lists (AC: 2)
  - [x] 5.2 Test `TypeCatalog` page renders Events grid with all required columns (AC: 3)
  - [x] 5.3 Test `TypeCatalog` page renders Commands grid with all required columns (AC: 4)
  - [x] 5.4 Test `TypeCatalog` page renders Aggregates grid with all required columns (AC: 5)
  - [x] 5.5 Test `TypeDetailPanel` renders event detail with related commands from same domain (AC: 8)
  - [x] 5.6 Test `TypeDetailPanel` renders command detail with target aggregate link (AC: 8)
  - [x] 5.7 Test `TypeDetailPanel` renders aggregate detail with event/command counts and projections badge (AC: 8)
  - [x] 5.8 Test page shows `IssueBanner` on API failure (AC: 10)
  - [x] 5.9 Test page shows `EmptyState` when no types returned (AC: 10)
  - [x] 5.10 Test rejection badge renders for rejection event types and is hidden for normal events (AC: 3)
  - **Recommended tests**:
  - [x] 5.11 Test search filter narrows displayed types by TypeName (AC: 6)
  - [x] 5.12 Test domain filter updates displayed types across all tabs (AC: 7)
  - [x] 5.13 Test tab switching updates active grid and preserves filters (AC: 1)
  - [x] 5.14 Test deep link `/types?tab=commands&domain=payments` restores correct tab and filter (AC: 9)
  - [x] 5.17 Test deep link `/types?tab=events&type=OrderCreated` opens detail panel for matching event (AC: 9)
  - [x] 5.15 Test HasProjections badge renders green "Yes" / neutral "No" correctly (AC: 5)
  - [x] 5.16 Test TargetAggregateType click in Commands grid navigates to Aggregates tab (AC: 4)

- [x] **Task 6: Polish and responsive layout** (AC: 11, 12)
  - [x] 6.1 Verify responsive detail panel layout (side panel on wide, below grid on narrow).
  - [x] 6.2 Verify all ARIA labels present on grids, search, tabs.
  - [x] 6.3 Verify monospace CSS class applied to all TypeName columns.
  - [x] 6.4 **Checkpoint**: All ACs pass, zero warnings, responsive layout works.

## Dev Notes

### Architecture Compliance

- **ADR-P4**: Admin.UI communicates exclusively via HTTP REST API to Admin.Server. No DaprClient in UI. The `AdminTypeCatalogApiClient` wraps `HttpClient` calls — follows identical pattern to `AdminStreamApiClient` and `AdminProjectionApiClient`.
- **SEC-5**: No event payload data in this story — type catalog contains only type metadata (names, domains, counts), not event content. No SEC-5 risk.
- **NFR46**: Type catalog (read) requires `ReadOnly` policy. No write operations in this story. All 3 API endpoints already enforce `[Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]`.
- **Tenant-agnostic**: Type catalog is NOT tenant-scoped. Types are registered globally via reflection-based assembly scanning. No tenantId parameter on any API call (per `ITypeCatalogService` docs). This differs from Projections and Streams which are tenant-scoped.

### FR74 Scope — Schemas and Version History Deferred

FR74 specifies: "browse all registered event types, command types, and aggregate types with their **schemas, relationships, and version history**." This story implements **relationships** (cross-tab linking, related types in detail panel) and **current version** (`SchemaVersion` column). **Schemas** (JSON Schema or sample payload) and **version history** (changelog between schema versions) are deferred — they require new server-side endpoints (`GET /api/v1/admin/types/events/{typeName}/schema`) and DTO extensions that do not exist yet. Document this as a follow-up story if schema browsing becomes a priority.

### FluentUI Tabs API — Verify at Build Time

The FluentTabs pattern in this story uses `ActiveTabId` / `ActiveTabIdChanged`. The exact Blazor binding API for FluentUI v4.13.2 `FluentTabs` may differ — check the [FluentUI Blazor documentation](https://www.fluentui-blazor.net/) at implementation time. If the API uses `@bind-ActiveTabId` natively, use that. If it uses separate property + callback, wire `ActiveTabIdChanged` to the `OnTabChange` handler. The component renders to `<fluent-tabs>` web component internally.

### Existing Code to Reuse (DO NOT Recreate)

| What | Where | How |
|------|-------|-----|
| `EventTypeInfo` record | `src/Hexalith.EventStore.Admin.Abstractions/Models/TypeCatalog/EventTypeInfo.cs` | Fields: TypeName, Domain, IsRejection (bool), SchemaVersion (int) |
| `CommandTypeInfo` record | `src/Hexalith.EventStore.Admin.Abstractions/Models/TypeCatalog/CommandTypeInfo.cs` | Fields: TypeName, Domain, TargetAggregateType |
| `AggregateTypeInfo` record | `src/Hexalith.EventStore.Admin.Abstractions/Models/TypeCatalog/AggregateTypeInfo.cs` | Fields: TypeName, Domain, EventCount (int), CommandCount (int), HasProjections (bool) |
| `ITypeCatalogService` interface | `src/Hexalith.EventStore.Admin.Abstractions/Services/ITypeCatalogService.cs` | `ListEventTypesAsync`, `ListCommandTypesAsync`, `ListAggregateTypesAsync` — all accept optional domain filter |
| `AdminTypeCatalogController` | `src/Hexalith.EventStore.Admin.Server/Controllers/AdminTypeCatalogController.cs` | Full REST API — 3 endpoints at `/api/v1/admin/types/{events\|commands\|aggregates}` |
| `DaprTypeCatalogService` | `src/Hexalith.EventStore.Admin.Server/Services/DaprTypeCatalogService.cs` | DAPR state store implementation using key pattern `admin:type-catalog:{type}:{domain\|all}` |
| `AdminStreamApiClient` | `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs` | Reference pattern for HTTP client, error handling, method signatures |
| `AdminProjectionApiClient` | `src/Hexalith.EventStore.Admin.UI/Services/AdminProjectionApiClient.cs` | Reference pattern (newest, follows same conventions) |
| `StatCard` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/StatCard.razor` | Summary stat cards with severity colors |
| `EmptyState` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/EmptyState.razor` | No data / error empty states |
| `IssueBanner` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/IssueBanner.razor` | API failure warnings |
| `SkeletonCard` component | `src/Hexalith.EventStore.Admin.UI/Components/Shared/SkeletonCard.razor` | Loading placeholders |
| `DashboardRefreshService` | `src/Hexalith.EventStore.Admin.UI/Services/DashboardRefreshService.cs` | Real-time refresh — subscribe to `OnDataChanged`. The signal does NOT include type data — handler must independently call all 3 `ListXxxAsync` methods. |
| `ViewportService` | `src/Hexalith.EventStore.Admin.UI/Services/ViewportService.cs` | Responsive layout: `IsWideViewport` for detail panel placement |
| `NavMenu.razor` | `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor` | Add Types nav link |
| `CommandPaletteCatalog.cs` | `src/Hexalith.EventStore.Admin.UI/Components/CommandPaletteCatalog.cs` | Add type catalog command palette entries |

### API Endpoints Used

```
# List event types (existing, ReadOnly policy):
GET /api/v1/admin/types/events?domain={domain}
                                    → IReadOnlyList<EventTypeInfo>

# List command types (existing, ReadOnly policy):
GET /api/v1/admin/types/commands?domain={domain}
                                    → IReadOnlyList<CommandTypeInfo>

# List aggregate types (existing, ReadOnly policy):
GET /api/v1/admin/types/aggregates?domain={domain}
                                    → IReadOnlyList<AggregateTypeInfo>
```

**All 3 endpoints are already implemented in `AdminTypeCatalogController`.** This story only creates the UI client and Blazor components — no server-side changes needed.

### AdminTypeCatalogApiClient — Pattern Reference

Follow `AdminProjectionApiClient` patterns exactly:
- Primary constructor with `IHttpClientFactory` and `ILogger<T>`
- Named HttpClient `"AdminApi"` via `httpClientFactory.CreateClient("AdminApi")`
- `using HttpResponseMessage response = await client.GetAsync(...)`
- `ReadFromJsonAsync<T>()` for deserialization
- URL construction: base path `/api/v1/admin/types/{events|commands|aggregates}` with optional `?domain=` query param
- **Error status mapping via `HandleErrorStatus` helper** (replicate from `AdminStreamApiClient`): 401 → `UnauthorizedAccessException`, 403 → `ForbiddenAccessException`, 503 → `ServiceUnavailableException`. Extract or copy this private helper method.
- Return empty defaults (`[]` for lists) on non-auth errors
- All methods `virtual` for NSubstitute mocking
- All methods use `ConfigureAwait(false)` and accept `CancellationToken`

### Domain Filter Query String Construction

The domain filter is passed as a query string parameter to the API. When domain is null, omit the query parameter entirely:
```csharp
string url = domain is not null
    ? $"/api/v1/admin/types/events?domain={Uri.EscapeDataString(domain)}"
    : "/api/v1/admin/types/events";
```

### Parallel Data Loading

Load all three type lists simultaneously in `OnInitializedAsync`:
```csharp
var eventsTask = _apiClient.ListEventTypesAsync(null, ct);
var commandsTask = _apiClient.ListCommandTypesAsync(null, ct);
var aggregatesTask = _apiClient.ListAggregateTypesAsync(null, ct);
await Task.WhenAll(eventsTask, commandsTask, aggregatesTask).ConfigureAwait(false);
_allEvents = eventsTask.Result;
_allCommands = commandsTask.Result;
_allAggregates = aggregatesTask.Result;
```

### Client-Side Search and Filtering

Type catalog data is small (typically < 100 types). All filtering is client-side:
```csharp
private IReadOnlyList<EventTypeInfo> FilteredEvents => _allEvents
    .Where(e => _domainFilter is null || e.Domain == _domainFilter)
    .Where(e => string.IsNullOrEmpty(_searchText) || e.TypeName.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
    .ToList();
```

### Search Debounce Pattern

Use a `CancellationTokenSource` for debouncing search input:
```csharp
private CancellationTokenSource? _debounceTokenSource;

private async Task OnSearchInput(ChangeEventArgs e)
{
    _debounceTokenSource?.Cancel();
    _debounceTokenSource = new CancellationTokenSource();
    try
    {
        await Task.Delay(300, _debounceTokenSource.Token).ConfigureAwait(false);
        await InvokeAsync(() =>
        {
            _searchText = e.Value?.ToString() ?? string.Empty;
            UpdateUrl();
            StateHasChanged();
        }).ConfigureAwait(false);
    }
    catch (TaskCanceledException) { }
}
```

### FluentTabs Pattern

```razor
<FluentTabs ActiveTabId="@_activeTab" OnTabChange="@OnTabChange">
    <FluentTab Id="events" Label="Events (@FilteredEvents.Count)">
        @* Events grid *@
    </FluentTab>
    <FluentTab Id="commands" Label="Commands (@FilteredCommands.Count)">
        @* Commands grid *@
    </FluentTab>
    <FluentTab Id="aggregates" Label="Aggregates (@FilteredAggregates.Count)">
        @* Aggregates grid *@
    </FluentTab>
</FluentTabs>
```

### Page Lifecycle Pattern (from stories 15-2 through 15-5)

```csharp
@implements IAsyncDisposable

protected override async Task OnInitializedAsync()
{
    ReadUrlParameters();
    await LoadDataAsync().ConfigureAwait(false);
    RefreshService.OnDataChanged += OnRefreshSignal;
}

// DashboardRefreshService.OnDataChanged is Action<DashboardData>?, NOT EventHandler
private void OnRefreshSignal(DashboardData data)
{
    try
    {
        _ = InvokeAsync(async () =>
        {
            await LoadDataAsync().ConfigureAwait(false);
            StateHasChanged();
        });
    }
    catch (ObjectDisposedException) { }
}

public async ValueTask DisposeAsync()
{
    _debounceTokenSource?.Cancel();
    _debounceTokenSource?.Dispose();
    RefreshService.OnDataChanged -= OnRefreshSignal;
    GC.SuppressFinalize(this);
}
```

### Responsive Detail Panel (from story 15-3/15-4/15-5 pattern)

```razor
<div style="display: flex; gap: 16px; @(Viewport.IsWideViewport ? "flex-direction: row;" : "flex-direction: column;")">
    <div style="@(Viewport.IsWideViewport ? "flex: 1; min-width: 0;" : "")">
        @* Tabs with grids *@
    </div>
    @if (HasSelection)
    {
        <div style="@(Viewport.IsWideViewport ? "width: 420px; flex-shrink: 0;" : "")">
            <TypeDetailPanel SelectedEvent="_selectedEvent"
                             SelectedCommand="_selectedCommand"
                             SelectedAggregate="_selectedAggregate"
                             AllEvents="_allEvents"
                             AllCommands="_allCommands"
                             AllAggregates="_allAggregates"
                             OnClose="ClearSelection"
                             OnNavigateToAggregate="NavigateToAggregate" />
        </div>
    }
</div>
```

### Cross-Tab Navigation (TargetAggregateType → Aggregates tab)

When a user clicks a `TargetAggregateType` link in the Commands grid:
1. Switch `_activeTab` to "aggregates"
2. Set `_searchText` to the aggregate type name
3. Update URL: `?tab=aggregates&search={aggregateTypeName}`
4. Select the matching aggregate in the grid if found

### Monospace Type Names

Apply a CSS class for monospace rendering of type names in grids:
```css
.type-name-cell {
    font-family: var(--body-font-monospace, 'Cascadia Code', 'Consolas', monospace);
    font-size: 0.85em;
}
```

### Key Differences from Projections Page (15-5)

| Aspect | Projections (15-5) | Type Catalog (15-6) |
|--------|-------------------|---------------------|
| Tenant scope | Tenant-scoped (tenantId param) | Tenant-agnostic (no tenantId) |
| Tab structure | No tabs, single grid | 3 tabs (events, commands, aggregates) |
| Write operations | Pause/resume/reset/replay | None — read-only |
| Detail panel | Single type (ProjectionDetail) | 3 types pattern-matched |
| Search | No text search | Client-side text search with debounce |
| Data loading | Single list API call | 3 parallel API calls |
| Authorization | Operator controls gated | No gated controls (all ReadOnly) |

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
    AdminTypeCatalogApiClient.cs         # NEW
  Pages/
    TypeCatalog.razor                    # NEW
    TypeCatalog.razor.css                # NEW
  Components/
    TypeDetailPanel.razor                # NEW
    TypeDetailPanel.razor.css            # NEW

tests/Hexalith.EventStore.Admin.UI.Tests/
  Pages/
    TypeCatalogPageTests.cs              # NEW
  Components/
    TypeDetailPanelTests.cs              # NEW
```

Modify existing files:
- `Layout/NavMenu.razor` — add Types nav link
- `Components/CommandPaletteCatalog.cs` — add type catalog entries
- `Program.cs` — register `AdminTypeCatalogApiClient`

### Previous Story Intelligence (15-5 Learnings)

- **bUnit test pattern**: Tests use NSubstitute to mock API clients. Override `virtual` methods with `Substitute.For<T>()`. Mock data should cover both populated and empty states.
- **FluentDataGrid column access in tests**: Use `cut.Find("fluent-data-grid")` and verify column headers via rendered HTML. Don't try to access FluentDataGrid internals directly.
- **Detail panel responsive layout**: The flex container pattern with `ViewportService.IsWideViewport` works reliably. Maintain the 420px side panel width for consistency with other pages.
- **DashboardRefreshService subscription**: The `OnDataChanged` event carries `DashboardData(Health, Streams)` — it does NOT include type catalog data. The handler must independently re-fetch all three type lists.
- **URL parameter management**: Use `NavigationManager.NavigateTo(url, replace: true)` for filter/tab changes. Parse all URL params in `OnInitializedAsync` to restore state on deep link navigation.
- **StatusBadge not needed here**: Type catalog uses simple `FluentBadge` for boolean indicators (Rejection, HasProjections), not the `StatusBadge` component which is designed for state workflows.

### References

- [Source: _bmad-output/planning-artifacts/prd.md — FR74]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR41, UX-DR42]
- [Source: _bmad-output/planning-artifacts/architecture.md — ADR-P4, D3 event type naming conventions]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-03-21-admin-tooling.md — Epic 15, Story 15.6]
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/AdminTypeCatalogController.cs — API endpoints]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/TypeCatalog/ — domain models]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Services/ITypeCatalogService.cs — service interface]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- FluentUI v4.13.2 FluentTabs API uses `ActiveTabId` + `ActiveTabIdChanged` (confirmed via existing prototype in Direction2.razor)
- FluentAnchor requires `Href` parameter; used styled `<span role="link">` for TargetAggregateType click handler instead
- Razor inline `@{ var ... }` inside `@if` blocks causes parser errors; moved to computed properties in `@code` block

### Completion Notes List

- **Task 1**: Created `AdminTypeCatalogApiClient` following exact `AdminProjectionApiClient` pattern. 3 virtual methods for events/commands/aggregates. HandleErrorStatus helper. Registered in Program.cs.
- **Task 2**: Created `TypeCatalog.razor` page at `/types` with FluentTabs (3 tabs), StatCards (3x), FluentSearch with 300ms debounce, FluentSelect domain filter, FluentDataGrid per tab with correct columns and sorting. URL parameter management, DashboardRefreshService subscription, loading/error/empty states. Cross-tab navigation for TargetAggregateType.
- **Task 3**: Created `TypeDetailPanel.razor` with typed parameters (no `object`). Event/Command/Aggregate detail views with related types. `RelatedTypeList.razor` helper with "Show all N" expansion pattern. Scoped CSS.
- **Task 4**: Added Types nav link between Projections and Services. Added 4 command palette entries.
- **Task 5**: Created 18 bUnit tests (11 page tests + 7 component tests). All merge-blocking and recommended tests passing. Full regression suite green (145/145).
- **Task 6**: Responsive layout verified (flex-direction row/column based on IsWideViewport). All ARIA labels present. Monospace CSS class applied to TypeName columns.

### File List

New files:
- `src/Hexalith.EventStore.Admin.UI/Services/AdminTypeCatalogApiClient.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor`
- `src/Hexalith.EventStore.Admin.UI/Pages/TypeCatalog.razor.css`
- `src/Hexalith.EventStore.Admin.UI/Components/TypeDetailPanel.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/TypeDetailPanel.razor.css`
- `src/Hexalith.EventStore.Admin.UI/Components/RelatedTypeList.razor`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TypeCatalogPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/TypeDetailPanelTests.cs`

Modified files:
- `src/Hexalith.EventStore.Admin.UI/Program.cs` — added AdminTypeCatalogApiClient DI registration
- `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor` — added Types nav link
- `src/Hexalith.EventStore.Admin.UI/Components/CommandPaletteCatalog.cs` — added 4 type catalog entries
