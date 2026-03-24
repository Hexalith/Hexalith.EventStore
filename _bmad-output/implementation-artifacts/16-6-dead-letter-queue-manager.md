# Story 16.6: Dead-Letter Queue Manager

Status: ready-for-dev

Size: Medium — ~8 new/modified files, 5 task groups, 14 ACs, ~26 tests (~8-12 hours estimated). Core new work: AdminDeadLetterApiClient (Task 1), full DeadLetters.razor page replacing the placeholder (Task 2), NavMenu entry (Task 3), bUnit tests (Task 4), Aspire verification (Task 5). Backend already implemented: DeadLetterEntry DTO, IDeadLetterQueryService, IDeadLetterCommandService, DaprDeadLetterQueryService, DaprDeadLetterCommandService, AdminDeadLettersController.

**Split advisory (recommended):** Deliver in two PRs. (A) Tasks 1-2 partial (grid + filters + pagination + row expansion + failure category presets, no selection) + Task 3 + Task 4 partial + Task 5, then (B) Task 2 remainder (checkbox selection + bulk action dialogs) + remaining Task 4 tests. The natural split point is between "dead-letter visibility" (browse, search, paginate, inspect) and "dead-letter operations" (select, retry, skip, archive). Both halves are independently shippable. PR A delivers immediate value for Alex's diagnostic workflow; PR B adds the operational bulk actions.

## Definition of Done

- All 14 ACs verified
- 13 merge-blocking bUnit tests green (Task 4 blocking tests: 4.1-4.13)
- 13 recommended bUnit tests green (Task 4 recommended tests: 4.14-4.26)
- Project builds with zero warnings in CI (`dotnet build --configuration Release`)
- No new analyzer suppressions

## Story

As a **support engineer (Alex) or operator using the Hexalith EventStore admin dashboard**,
I want **a dead-letter queue manager page where I can browse failed commands with filtering by tenant and search, view failure details, and perform bulk operations (retry, skip, archive) on selected entries**,
so that **I can diagnose command failures, recover from infrastructure issues by retrying commands, clean up known-bad messages by skipping or archiving them, and maintain dead-letter queue health — all through the admin UI without developer involvement (FR78)**.

## Acceptance Criteria

1. **Dead-letter entry list** — The `/health/dead-letters` page displays a `FluentDataGrid` listing dead-letter entries from `AdminDeadLetterApiClient.GetDeadLettersAsync`. Columns: checkbox (for bulk selection), Message ID (monospace, truncated to 8 chars with full ID in tooltip), Tenant ID (monospace), Domain, Aggregate ID (monospace, truncated to 8 chars), Command Type (short name — strip namespace, show full in tooltip), Failure Reason (truncated to 60 chars, full text in tooltip), Failed At (relative time using `TimeFormatHelper.FormatRelativeTime`, full UTC on tooltip), Retry Count (right-aligned). Grid is sortable by Failed At (default descending), Tenant ID, Domain, Command Type, Retry Count. When no dead letters exist, show `EmptyState` with title "No dead letters. All commands processed successfully." and description "Failed commands will appear here for investigation and replay."

2. **Summary stat cards** — Above the grid, display four stat cards in a `FluentGrid` (xs=6, sm=6, md=3): (a) "Total Dead Letters" showing total count from `PagedResult.TotalCount`, (b) "Tenants Affected" showing distinct tenant count from loaded entries, severity Warning when > 0, (c) "Oldest Entry" showing relative time of the oldest `FailedAtUtc` entry, or "None" when empty, (d) "High Retry (3+)" showing count of entries with `RetryCount >= 3`, severity Error when > 0. Loading state shows 4 `SkeletonCard` placeholders.

3. **Tenant filter and search** — A `FluentTextField` with debounced input (300ms) filters dead letters by tenant ID prefix. Filter persists in URL query parameter `?tenant=<id>`. Same debounce pattern as `Compaction.razor` (Timer + `InvokeAsync` for Blazor Server threading). Additionally, a `FluentTextField` for search (debounced 300ms) filters by command type substring or failure reason substring (client-side on loaded data). Persists as `?search=<value>`. **Failure category presets:** A `FluentSelect` dropdown before the search field offers common presets: "All" (default, clears search), "Timeout", "Deserialization", "Authorization", "Other". Selecting a preset populates the search field with the corresponding substring (e.g., "timeout", "deserialization", "authorization"). "Other" inverts — filters to entries whose FailureReason does NOT contain any of the known category substrings. This is purely client-side UI sugar — no backend changes. Persists as `?category=<value>` in URL.

4. **Pagination** — The API returns `PagedResult<DeadLetterEntry>` with `ContinuationToken`. Display a "Load More" button at the bottom of the grid when `ContinuationToken` is not null. Clicking it appends the next page to the existing list. Default page size: 100 entries. Show total count from `PagedResult.TotalCount` above the grid: "Showing {loaded} of {total} entries". **Deduplication:** After each Load More append, deduplicate `_allEntries` by `MessageId` (DAPR eventual consistency may shift entries between pages). Use a `HashSet<string>` of loaded IDs to skip duplicates. **Filter change resets pagination:** When the tenant filter changes, clear `_allEntries`, reset `_continuationToken` to null, and reload from page 1 with the new tenant filter. The search filter is client-side only and does NOT reset pagination (it filters the already-loaded `_allEntries`).

5. **Entry detail expansion** — Clicking a row (not the checkbox) expands an inline detail section below the row (same pattern as failed-job error detail in Compaction.razor). Detail shows: full Message ID (monospace, copyable), Tenant ID, Domain, full Aggregate ID (monospace, copyable), full Command Type, Correlation ID (monospace, copyable), full Failure Reason in a `FluentCard` with monospace font and `pre-wrap`, Failed At (full UTC timestamp), Retry Count. Click again to collapse.

6. **Bulk selection** — A header checkbox in the grid toggles select-all for the visible (filtered) entries in `_filteredEntries` only — never `_allEntries`. The header checkbox uses tri-state: unchecked (none selected), indeterminate (some selected), checked (all visible selected). Use `FluentCheckbox` with `CheckState` bound to a computed property. Individual row checkboxes toggle per-entry selection. A selection summary bar appears when any entries are selected: "**{count}** selected" with action buttons. Clearing filters or changing tenant filter resets `_selectedIds` to empty. **FluentUI gotcha:** `FluentDataGrid.OnRowClick` fires on the entire `<tr>`. Checkbox clicks inside a `TemplateColumn` will trigger row expansion unless stopped. Wrap each `FluentCheckbox` in `<div @onclick:stopPropagation="true">` to prevent the checkbox click from also triggering the row expansion handler (AC 5).

7. **Retry action** — A "Retry Selected" button (visible only to `AdminRole.Operator` via `AuthorizedView MinimumRole="AdminRole.Operator"`) appears in the selection summary bar. Clicking opens a confirmation dialog: "Retry {count} dead-letter command(s)? These commands will be resubmitted for processing." Dialog shows per-tenant progress during execution: "Processing tenant 2 of 4..." Tenant grouping: entries are grouped by tenant ID and each group is retried in a separate API call (`POST api/v1/admin/dead-letters/{tenantId}/retry`). **Disable Refresh button** while `_isOperating` is true to prevent state races. On success: close dialog, **clear `_selectedIds`**, toast "Retried {count} dead-letter command(s).", reload from page 1. On partial failure (some tenants succeed, some fail): **clear `_selectedIds`**, toast with warning "Retried {successCount} commands. {failCount} failed: {errorMessage}", reload. On full failure: error toast, stay on dialog (selection preserved for retry).

8. **Skip action** — A "Skip Selected" button (Operator role) appears in the selection summary bar. Clicking opens a confirmation dialog: "Skip {count} dead-letter command(s)? These commands will be **permanently** removed from the dead-letter queue and will NOT be retried." On confirm: calls `SkipDeadLettersAsync` per tenant group. Same per-tenant progress, Refresh disable, selection clearing, and success/failure toast pattern as Retry (AC 7).

9. **Archive action** — An "Archive Selected" button (Operator role) appears in the selection summary bar. Clicking opens a confirmation dialog: "Archive {count} dead-letter command(s)? These commands will be moved to the archive. They can be reviewed later but will no longer appear in the active queue." On confirm: calls `ArchiveDeadLettersAsync` per tenant group. Same per-tenant progress, Refresh disable, selection clearing, and success/failure toast pattern as Retry (AC 7).

10. **URL state persistence** — The `/health/dead-letters` page persists filter state in URL query parameters: `?tenant=<id>&search=<text>`. Page loads with filters pre-applied from URL. Uses `NavigationManager.NavigateTo(url, forceLoad: false, replace: true)`. All values escaped with `Uri.EscapeDataString()`.

11. **Navigation entry** — Add a "Dead Letters" `FluentNavLink` in `NavMenu.razor` positioned after "Health" (line 14). Use `Icons.Regular.Size20.DocumentDismiss` icon (try first; if missing, try `Icons.Regular.Size20.ErrorCircle` or `Icons.Regular.Size20.Warning`). Href: `/health/dead-letters`. Verify `CommandPaletteCatalog.cs` already has "Dead Letters" entry (it does — confirm at build time). Verify `Breadcrumb.razor` already maps `dead-letters` to "Dead Letters" (it does — line 64).

12. **Data loading and refresh** — Initial data loads in `OnInitializedAsync` via `AdminDeadLetterApiClient`. Manual refresh via "Refresh" button (clears selection, reloads from first page). Refresh button is disabled while `_isOperating` is true (bulk action in flight) to prevent concurrent state mutations. Error state shows `IssueBanner` with "Unable to load dead-letter entries" and retry button. `IAsyncDisposable` for cleanup (dispose `_debounceTimer`, cancel `_loadCts`). **Disposed guard:** The debounce timer callback MUST check `_disposed` before calling `InvokeAsync`, same as `Compaction.razor` line 324 — prevents `ObjectDisposedException` when navigating away during pending debounce.

13. **Operator role enforcement** — The `/health/dead-letters` page is visible to all authenticated users (`ReadOnly` minimum) for viewing dead-letter entries and details. ALL write operations (retry, skip, archive) require `Operator` role (minimum). Write buttons are hidden for non-Operator users via `AuthorizedView MinimumRole="AdminRole.Operator"`. API calls for writes use `AdminOperator` policy. Note: Operator (not Admin) is the correct minimum — dead-letter management is an operational task per AdminRole definition.

14. **Accessibility** — Page heading `<h1>Dead Letters</h1>` is the first focusable element. All dialogs use `FluentDialog` with proper `aria-label`. Checkboxes have `aria-label="Select {messageId}"`. Selection summary announces count via `aria-live="polite"`. Stat cards follow existing `StatCard` accessibility pattern. Data grid follows `FluentDataGrid` built-in accessibility. Status text and failure reasons use semantic markup.

## Tasks / Subtasks

- [ ] **Task 1: Create AdminDeadLetterApiClient** (AC: 1, 4, 7, 8, 9, 12)
  - [ ] 1.1 Create `src/Hexalith.EventStore.Admin.UI/Services/AdminDeadLetterApiClient.cs` — follow the exact pattern of `AdminCompactionApiClient.cs`:
    ```csharp
    public class AdminDeadLetterApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<AdminDeadLetterApiClient> logger)
    ```
    Named HttpClient: `"AdminApi"`. All methods virtual for testing.
  - [ ] 1.2 `GetDeadLettersAsync(string? tenantId, int count = 100, string? continuationToken = null, CancellationToken ct)` — calls `GET api/v1/admin/dead-letters?tenantId={}&count={}&continuationToken={}`. Returns `PagedResult<DeadLetterEntry>`. Omit query params when null/default.
  - [ ] 1.3 `RetryDeadLettersAsync(string tenantId, IReadOnlyList<string> messageIds, CancellationToken ct)` — calls `POST api/v1/admin/dead-letters/{tenantId}/retry`. Body: serialize as `{ "messageIds": [...] }` — the server deserializes into `DeadLetterActionRequest` (defined at `src/Hexalith.EventStore.Admin.Server/Models/DeadLetterActionRequest.cs`, a record with `[Required] IReadOnlyList<string> MessageIds`). Use `JsonContent.Create(new { MessageIds = messageIds })`. Returns `AdminOperationResult`.
  - [ ] 1.4 `SkipDeadLettersAsync(string tenantId, IReadOnlyList<string> messageIds, CancellationToken ct)` — calls `POST api/v1/admin/dead-letters/{tenantId}/skip`. Same body/return pattern as 1.3.
  - [ ] 1.5 `ArchiveDeadLettersAsync(string tenantId, IReadOnlyList<string> messageIds, CancellationToken ct)` — calls `POST api/v1/admin/dead-letters/{tenantId}/archive`. Same body/return pattern as 1.3.
  - [ ] 1.6 Error handling: 401 → `UnauthorizedAccessException`, 403 → `ForbiddenAccessException`, 503 → `ServiceUnavailableException`. All other HTTP errors → `ServiceUnavailableException` (log and wrap). Same pattern as `AdminCompactionApiClient.HandleErrorStatus`.
  - [ ] 1.7 Register `AdminDeadLetterApiClient` in DI. Add `builder.Services.AddScoped<AdminDeadLetterApiClient>();` in `src/Hexalith.EventStore.Admin.UI/Program.cs` after line 40 (where `AdminBackupApiClient` is registered). Follow the exact same `AddScoped` pattern as the other API clients on lines 37-40.

- [ ] **Task 2: Implement DeadLetters.razor page** (AC: 1-10, 12, 13, 14)
  - [ ] 2.1 Replace the placeholder in `src/Hexalith.EventStore.Admin.UI/Pages/DeadLetters.razor`. Route: `@page "/health/dead-letters"`. Inject: `AdminDeadLetterApiClient`, `NavigationManager`, `IToastService`. Implement `IAsyncDisposable`.
  - [ ] 2.2 **Header bar** — Title "Dead Letters" with action buttons: "Refresh" (outline, all users). No create/trigger button needed (dead letters are created by the system, not operators).
  - [ ] 2.3 **IssueBanner** for API error state — same pattern as Compaction.razor.
  - [ ] 2.4 **Filter bar** — Three filter controls: (1) `FluentTextField` for tenant filter (`?tenant=`), debounced 300ms, passed to API call. (2) `FluentSelect` for failure category presets: "All", "Timeout", "Deserialization", "Authorization", "Other" (`?category=`). Selecting a preset populates the search field; "Other" inverts match. (3) `FluentTextField` for search filter (`?search=`), debounced 300ms, client-side filter on `OriginalCommandType` and `FailureReason` (case-insensitive `Contains`). Tenant filter change resets pagination + selection; search filter and category only filter `_filteredEntries` client-side.
  - [ ] 2.5 **Stat cards** — Four `StatCard` in `FluentGrid` per AC 2. Computed from loaded entries list.
  - [ ] 2.6 **DataGrid with checkboxes** — `FluentDataGrid` with `TGridItem="DeadLetterEntry"`. Add a `TemplateColumn` for checkbox as the first column. Per AC 1.
  - [ ] 2.7 **Row expansion** — Track `_expandedEntry` (nullable `DeadLetterEntry`). `OnRowClick` toggles expansion. Show `FluentCard` below expanded row with full detail per AC 5. Ensure clicking the checkbox does NOT trigger expansion (use `@onclick:stopPropagation` on checkbox).
  - [ ] 2.8 **Selection state** — `HashSet<string> _selectedIds` tracks selected message IDs. Header checkbox toggles all IDs in `_filteredEntries` (never `_allEntries`). Header uses tri-state: unchecked/indeterminate/checked via computed `CheckState`. Individual checkboxes toggle one. Selection summary bar with count and action buttons per AC 6. Clear `_selectedIds` on: filter change, tenant filter change, bulk action completion (success or partial).
  - [ ] 2.9 **Bulk action dialogs** — Three `FluentDialog` instances (retry, skip, archive) per AC 7-9. Group selected entries by TenantId (`_allEntries.Where(e => _selectedIds.Contains(e.MessageId)).GroupBy(e => e.TenantId)`) and call API per tenant. Show per-tenant progress in dialog: "Processing tenant 2 of 4..." Handle partial success. Clear `_selectedIds` after completion (success or partial). Disable Refresh button while `_isOperating`.
  - [ ] 2.10 **Pagination** — "Load More" button when `ContinuationToken != null`. Appends to `_allEntries` list. Shows "Showing X of Y entries" text. Per AC 4.
  - [ ] 2.11 **URL state** — `ReadUrlParameters()` on init, `UpdateUrl()` on filter change. Per AC 10.
  - [ ] 2.12 **Data loading** — `LoadDataAsync()` with `CancellationTokenSource` pattern. Error handling for `UnauthorizedAccessException`, `ForbiddenAccessException`, `ServiceUnavailableException`, `HttpRequestException`. Per AC 12.
  - [ ] 2.13 **Auth guards** — Wrap all action buttons in `AuthorizedView MinimumRole="AdminRole.Operator"`. Per AC 13.
  - [ ] 2.14 **Accessibility** — `aria-label` on all interactive elements, `aria-live="polite"` on selection summary. Per AC 14.

  **State variables:**
  ```csharp
  private List<DeadLetterEntry> _allEntries = [];
  private IReadOnlyList<DeadLetterEntry> _filteredEntries = [];
  private bool _isLoading = true;
  private bool _apiUnavailable;
  private bool _disposed;
  private string _apiErrorMessage = "...";
  private string? _tenantFilter;
  private string? _tenantFilterInput;
  private string? _searchFilter;
  private string? _searchFilterInput;
  private Timer? _debounceTimer;
  private CancellationTokenSource? _loadCts;
  private bool _isOperating;
  private DeadLetterEntry? _expandedEntry;
  private HashSet<string> _selectedIds = [];
  private HashSet<string> _loadedMessageIds = []; // deduplication guard for Load More
  private string? _continuationToken;
  private int _totalCount;
  private string? _failureCategory; // preset filter: "All", "Timeout", etc.

  // Dialog state
  private bool _showRetryDialog;
  private bool _showSkipDialog;
  private bool _showArchiveDialog;
  private int _bulkProgressCurrent; // per-tenant progress counter
  private int _bulkProgressTotal;
  ```

- [ ] **Task 3: Navigation entry in NavMenu.razor** (AC: 11)
  - [ ] 3.1 Add `<FluentNavLink Href="/health/dead-letters" Icon="@(new Icons.Regular.Size20.DocumentDismiss())">Dead Letters</FluentNavLink>` in `NavMenu.razor` after the Health link (after line 14). If `DocumentDismiss` doesn't compile, try `ErrorCircle` or `Warning`.
  - [ ] 3.2 Verify breadcrumb mapping exists: `["dead-letters"] = "Dead Letters"` in `Breadcrumb.razor` line 64 — already present.
  - [ ] 3.3 Verify `CommandPaletteCatalog.cs` has "Dead Letters" → `/health/dead-letters` entry — already present (line 14). Add a second entry for search: `new("Dead Letters", "Dead Letter Queue Manager", "/health/dead-letters")`.

- [ ] **Task 4: bUnit tests** (AC: 1-14)
  - [ ] **Merge-blocking tests:**
  - [ ] 4.1 `DeadLetters_ShowsLoadingSkeletons_WhenLoading` — Four `SkeletonCard` visible during initial load.
  - [ ] 4.2 `DeadLetters_ShowsEmptyState_WhenNoEntries` — `EmptyState` with correct title when API returns empty.
  - [ ] 4.3 `DeadLetters_ShowsDataGrid_WhenEntriesExist` — Grid renders with correct columns and entry data.
  - [ ] 4.4 `DeadLetters_ShowsStatCards_WhenLoaded` — Four stat cards with computed values (total, tenants, oldest, high retry).
  - [ ] 4.5 `DeadLetters_FiltersVisibleEntries_WhenTenantFilterApplied` — Tenant filter restricts visible rows.
  - [ ] 4.6 `DeadLetters_FiltersVisibleEntries_WhenSearchFilterApplied` — Search filter matches command type and failure reason.
  - [ ] 4.7 `DeadLetters_ExpandsRowDetail_OnRowClick` — Click shows full detail `FluentCard` below the row.
  - [ ] 4.8 `DeadLetters_ShowsRetryDialog_WhenRetryClicked` — Retry button opens confirmation dialog with correct count.
  - [ ] 4.9 `DeadLetters_CallsRetryApi_OnConfirm` — API called with correct tenant grouping; success toast shown.
  - [ ] 4.10 `DeadLetters_ShowsSkipDialog_WhenSkipClicked` — Skip button opens dialog with "permanently" bold.
  - [ ] 4.11 `DeadLetters_ShowsArchiveDialog_WhenArchiveClicked` — Archive button opens dialog.
  - [ ] 4.12 `DeadLetters_ShowsIssueBanner_WhenApiUnavailable` — Error banner shown on `ServiceUnavailableException`.
  - [ ] 4.13 `DeadLetters_CheckboxClick_DoesNotTriggerRowExpansion` — Clicking checkbox does NOT expand the detail row (stopPropagation guard).
  - [ ] **Recommended tests:**
  - [ ] 4.14 `DeadLetters_SelectAll_TogglesAllVisibleCheckboxes` — Header checkbox selects/deselects all.
  - [ ] 4.15 `DeadLetters_IndividualCheckbox_TogglesSelection` — Row checkbox adds/removes from selection.
  - [ ] 4.16 `DeadLetters_SelectionBar_ShowsCountAndButtons` — Selection summary bar with correct count.
  - [ ] 4.17 `DeadLetters_HidesActionButtons_ForReadOnlyUser` — No retry/skip/archive buttons for ReadOnly role.
  - [ ] 4.18 `DeadLetters_ShowsActionButtons_ForOperatorUser` — Buttons visible for Operator role.
  - [ ] 4.19 `DeadLetters_LoadMore_AppendsEntries` — "Load More" adds second page to list.
  - [ ] 4.20 `DeadLetters_HidesLoadMore_WhenNoContinuationToken` — No button when last page.
  - [ ] 4.21 `DeadLetters_PersistsFiltersInUrl` — URL updated with tenant/search query params.
  - [ ] 4.22 `DeadLetters_ReadsFiltersFromUrl_OnInit` — Filters pre-applied from URL on load.
  - [ ] 4.23 `DeadLetters_HandlesPartialFailure_OnRetry` — Warning toast on mixed success/failure.
  - [ ] 4.24 `DeadLetters_SelectionClearsOnFilterChange` — Changing tenant or search filter resets `_selectedIds` to empty.
  - [ ] 4.25 `DeadLetters_TenantFilterResetsPagination` — Changing tenant filter clears `_allEntries`, resets `_continuationToken`, and reloads from page 1.
  - [ ] 4.26 `DeadLetters_SkipAndArchive_GroupByTenantLikeRetry` — Skip and archive bulk actions group by TenantId and handle partial failure (smoke test for shared logic).

  Test file: `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/DeadLettersTests.cs` (follow pattern of existing bUnit test files in the same folder). Mock `AdminDeadLetterApiClient` (all methods are virtual).

- [ ] **Task 5: Aspire topology verification** (AC: 12)
  - [ ] 5.1 Run `dotnet build Hexalith.EventStore.slnx --configuration Release` — zero warnings.
  - [ ] 5.2 Verify the DeadLetters page loads in Aspire AppHost and connects to `AdminDeadLetterApiClient` → `AdminDeadLettersController` → DAPR services.

## Dev Notes

### Backend is ALREADY IMPLEMENTED — Do NOT recreate

The following files already exist and are complete. **Do NOT modify or recreate them:**
- `src/Hexalith.EventStore.Admin.Abstractions/Models/DeadLetters/DeadLetterEntry.cs` — DTO with 9 fields (MessageId, TenantId, Domain, AggregateId, CorrelationId, FailureReason, FailedAtUtc, RetryCount, OriginalCommandType)
- `src/Hexalith.EventStore.Admin.Abstractions/Services/IDeadLetterQueryService.cs` — `ListDeadLettersAsync(tenantId, count, continuationToken, ct)` → `PagedResult<DeadLetterEntry>`
- `src/Hexalith.EventStore.Admin.Abstractions/Services/IDeadLetterCommandService.cs` — `RetryDeadLettersAsync`, `SkipDeadLettersAsync`, `ArchiveDeadLettersAsync` (all take tenantId + messageIds)
- `src/Hexalith.EventStore.Admin.Server/Services/DaprDeadLetterQueryService.cs` — DAPR state store implementation
- `src/Hexalith.EventStore.Admin.Server/Services/DaprDeadLetterCommandService.cs` — DAPR service invocation implementation
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminDeadLettersController.cs` — REST API at `api/v1/admin/dead-letters`
- `src/Hexalith.EventStore.Admin.Server/Models/DeadLetterActionRequest.cs` — Request body with `MessageIds` list
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminDeadLettersControllerTests.cs` — Controller tests

### API Endpoints (already implemented)

| Method | Route | Auth Policy | Body |
|--------|-------|-------------|------|
| GET | `api/v1/admin/dead-letters?tenantId=&count=&continuationToken=` | ReadOnly | — |
| POST | `api/v1/admin/dead-letters/{tenantId}/retry` | Operator | `{ "messageIds": [...] }` |
| POST | `api/v1/admin/dead-letters/{tenantId}/skip` | Operator | `{ "messageIds": [...] }` |
| POST | `api/v1/admin/dead-letters/{tenantId}/archive` | Operator | `{ "messageIds": [...] }` |

### UI-only patterns to follow

| Pattern | Reference file |
|---------|---------------|
| API client structure | `src/Hexalith.EventStore.Admin.UI/Services/AdminCompactionApiClient.cs` |
| Page layout + stat cards + filter + grid + dialog | `src/Hexalith.EventStore.Admin.UI/Pages/Compaction.razor` |
| Debounce filter | `Compaction.razor` lines 316-343 (Timer + InvokeAsync) |
| URL persistence | `Compaction.razor` ReadUrlParameters/UpdateUrl methods |
| Error handling | `Compaction.razor` LoadDataAsync catch blocks |
| Row expansion | `Compaction.razor` lines 144-151 (expandedJob FluentCard) |
| Status badges | `Compaction.razor` GetStatusBadge method |
| Auth guards | `AuthorizedView MinimumRole="AdminRole.Operator"` |
| Shared components | `StatCard`, `SkeletonCard`, `EmptyState`, `IssueBanner`, `AuthorizedView`, `TimeFormatHelper` in `Components/Shared/` |

### Authorization: Operator, NOT Admin

Dead-letter queue management (retry/skip/archive) requires `AdminRole.Operator` minimum, not `AdminRole.Admin`. The `AdminRole` enum: `ReadOnly < Operator < Admin`. Backup/restore and tenant management require Admin; dead-letters and projections require Operator. The controller already enforces `AdminAuthorizationPolicies.Operator` for write endpoints.

### Pagination differs from other pages

Unlike Compaction/Backups/Snapshots pages that load all data at once, the dead-letter page uses server-side pagination via `PagedResult<T>` with `ContinuationToken`. Maintain a growing `_allEntries` list that accumulates pages. The "Load More" pattern avoids loading potentially thousands of dead-letter entries at once.

### Checkbox selection pattern (new for this page)

This is the first admin page with multi-row selection. Use a `HashSet<string>` keyed by `MessageId`. **Selection scope:** Header "select all" must iterate `_filteredEntries` only, never `_allEntries`. This prevents accidentally selecting entries hidden by the search filter. **Tri-state header checkbox:** Use `FluentCheckbox` with `CheckState` property: `Unchecked` (none selected), `Indeterminate` (some but not all visible selected), `Checked` (all visible selected). Compute from `_selectedIds.Count` vs `_filteredEntries.Count`. **Clear on state change:** Clear `_selectedIds` when tenant filter changes, search filter changes, or after any bulk action completes (success or partial — stale IDs may reference entries no longer in the DLQ). When executing bulk actions, group selected entries by `TenantId` (the API requires per-tenant calls). Handle partial success: if 3 tenant groups are processed and 1 fails, report both success and failure counts.

**Critical: stopPropagation on checkboxes.** `FluentDataGrid.OnRowClick` fires on the entire `<tr>`. Without prevention, clicking a checkbox also triggers row expansion. Wrap each `FluentCheckbox` in `<div @onclick:stopPropagation="true">` inside the `TemplateColumn`. The header select-all checkbox needs the same wrapper. Test this explicitly (test 4.13).

### DI registration

API clients are registered in `src/Hexalith.EventStore.Admin.UI/Program.cs` lines 37-40 as `AddScoped<T>()`. Add `AdminDeadLetterApiClient` on line 41 following the same pattern.

### Known limitations (out of scope — document, do not fix)

- **No tenant-level retry-all endpoint:** The API requires explicit `messageIds` in the request body. For incidents with 10,000+ dead letters, the operator must load and select entries manually. A future `POST /dead-letters/{tenantId}/retry-all` endpoint would solve this. Document as a candidate for a future story.
- **Load More degrades at scale:** The "Load More" pagination pattern works well up to ~500 entries. Beyond that, the operator should use tenant filters to scope results rather than loading all pages. This is acceptable for v1.
- **Actions dropdown vs three buttons:** The current design uses three separate action buttons (Retry, Skip, Archive) in the selection summary bar. A future UX iteration could consolidate these into a single "Actions" dropdown to reduce cognitive load during incidents. Not blocking for v1.

### Navigation placement

Dead Letters is at `/health/dead-letters` — it's a health/observability sub-page. Add it after the Health nav link in `NavMenu.razor`. Breadcrumb rendering: `Home / Health / Dead Letters` — the `/health` segment maps to "Health" and `/dead-letters` maps to "Dead Letters" (both already in the dictionary).

### Project Structure Notes

- All new files go in existing directories — no new directories needed
- `AdminDeadLetterApiClient.cs` → `src/Hexalith.EventStore.Admin.UI/Services/`
- `DeadLetters.razor` → `src/Hexalith.EventStore.Admin.UI/Pages/` (replace placeholder)
- `DeadLettersTests.cs` → `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/`
- NavMenu.razor edit → `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor`

### References

- [Source: _bmad-output/planning-artifacts/prd.md — FR78: dead-letter queue management with browse, search, retry, skip, archive, bulk operations]
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 16 — Admin Web UI DBA Operations, FR76/FR77/FR78]
- [Source: _bmad-output/planning-artifacts/epics.md#Story 3.4 — Dead-Letter Routing & Command Replay (FR8, FR6)]
- [Source: _bmad-output/planning-artifacts/prd.md#Alex's Monday Morning — dead-letter inspection/replay user journey]
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/AdminDeadLettersController.cs — existing REST API]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/DeadLetters/DeadLetterEntry.cs — existing DTO]
- [Source: src/Hexalith.EventStore.Admin.UI/Pages/Compaction.razor — reference page pattern]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
