# Story 16.3: Compaction Manager

Status: done

Size: Medium — ~15 new/modified files, 6 task groups, 13 ACs, ~20 tests (~8-12 hours estimated). Core new work: CompactionJob DTO + enum (Task 1), query endpoint + server impl (Task 2), AdminCompactionApiClient (Task 3), Compaction.razor page with single-step trigger dialog (Task 4), bUnit + FormatBytes unit tests (Task 5), nav + breadcrumb + CSS + FormatBytes extraction (Task 6).

## Definition of Done

- All 13 ACs verified
- 11 merge-blocking bUnit tests green (Task 5 blocking tests, including trigger-compaction happy/error paths)
- 9 recommended bUnit tests green (includes FormatBytes boundary tests)
- Project builds with zero warnings in CI (`dotnet build --configuration Release`)
- No new analyzer suppressions

## Story

As a **database administrator (Maria) using the Hexalith EventStore admin dashboard**,
I want **a compaction manager page where I can view compaction job history, trigger compaction for specific tenants and domains, see job status with space reclaimed metrics, and monitor active compaction operations**,
so that **I can reclaim storage space from compacted event streams, schedule maintenance compaction during low-traffic windows, and track compaction effectiveness — all through the admin UI without developer involvement**.

## Acceptance Criteria

1. **Compaction job list** — The `/compaction` page displays a `FluentDataGrid` listing all compaction jobs from `GetCompactionJobsAsync`. Columns: Tenant ID (monospace), Domain (or "All" when null), Status (badge — green Completed, orange Running, blue Pending, red Failed), Started (relative time using `TimeFormatHelper.FormatRelativeTime`, full UTC on tooltip), Duration (formatted as "Xm Ys" when completed, "Running..." when active, "—" when pending), Events Compacted (right-aligned, `N0`, "—" when pending/running), Space Reclaimed (formatted via `FormatBytes`, "—" when pending/running). Grid is sortable by all columns, default sort by Started descending (most recent first). When no jobs exist, show `EmptyState` with title "No compaction jobs" and description "Trigger a compaction to reclaim storage space from event streams."

2. **Summary stat cards** — Above the grid, display three stat cards in a `FluentGrid` (xs=6, sm=6, md=4): (a) "Active Jobs" showing the count of jobs with status Pending or Running, severity Warning when > 0, (b) "Completed (30d)" showing the count of jobs with status Completed and StartedAtUtc within the last 30 days, (c) "Space Reclaimed" showing the sum of `SpaceReclaimedBytes` across all completed jobs formatted via `FormatBytes`, or "N/A" when no completed jobs have space data. Loading state shows 3 `SkeletonCard` placeholders.

3. **Tenant filter** — A `FluentTextField` with debounced input (300ms) filters jobs by tenant ID prefix. Filter persists in URL query parameter `?tenant=<id>`. Clearing the filter shows all jobs. Same debounce pattern as `Storage.razor` (Timer + `InvokeAsync` for Blazor Server threading).

4. **Trigger compaction dialog** — A "Trigger Compaction" button (visible only to `Operator` role or above via `AuthorizedView`) opens a `FluentDialog` (single-step, matching Snapshots.razor delete dialog pattern). Dialog title: "Trigger Compaction". Form fields: Tenant ID (`FluentTextField`, required), Domain (`FluentTextField`, optional — leave empty to compact all domains in tenant). Below the form fields, a warning banner states: "Compaction is a resource-intensive operation that runs asynchronously. It may temporarily increase CPU and I/O usage. Consider running during low-traffic periods." Primary button "Start Compaction" (accent style) calls `TriggerCompactionAsync`. Secondary button "Cancel". Validate Tenant ID non-empty before enabling primary button. On success: close dialog, show toast "Compaction started for {tenantId}. Refresh to check status.", reload job list. On failure: show error toast with `AdminOperationResult.Message`, keep dialog open. **Concurrent guard:** If the loaded job list contains a Pending or Running job for the same tenant, disable the "Start Compaction" button and show inline text below the Tenant ID field: "Compaction already in progress for this tenant."

5. **Trigger dialog confirmation text** — The dialog body includes a dynamic confirmation sentence above the warning banner: "This will compact event streams for tenant **{tenantId}**{, domain **{domain}**}." This text updates live as the user types, giving clear feedback on what will be compacted. This satisfies the UX spec requirement for confirmation of resource-intensive operator-level actions without needing a separate confirmation step.

6. **Job status display** — Each job row displays a status badge using `FluentBadge`: Pending (blue, `Appearance.Accent`), Running (orange, `Appearance.Warning` — pulsing via CSS animation), Completed (green, `Appearance.Success`), Failed (red, `Appearance.Error`). Failed jobs show an expandable error message row below the main row (click to toggle). The error message is displayed in a `FluentCard` with monospace font.

7. **Space reclaimed formatting** — Space reclaimed values use a shared `FormatBytes` helper (same pattern as `Storage.razor`) that formats bytes as human-readable strings: "1.2 GB", "456 MB", "12 KB", etc. When the backend does not support size reporting (`SpaceReclaimedBytes` is null), display "N/A" with a tooltip "Backend does not report size metrics (NFR44)."

8. **URL state persistence** — The `/compaction` page persists filter state in URL query parameters: `?tenant=<id>` (tenant filter). Page loads with filter pre-applied from URL. Uses `NavigationManager.NavigateTo(url, forceLoad: false, replace: true)`. All values escaped with `Uri.EscapeDataString()`.

9. **Breadcrumb integration** — The breadcrumb route label dictionary in `Breadcrumb.razor` includes `"compaction" -> "Compaction"`. Navigating to `/compaction` renders breadcrumb: `Home / Compaction`.

10. **Navigation entry** — The compaction page appears in `NavMenu.razor` after "Snapshots", with a compaction-related icon. **Try `Icons.Regular.Size20.ArrowMinimize`** first; if it does not compile, try `Icons.Regular.Size20.Compress` or `Icons.Regular.Size20.BoxArrowDown`. Verify the icon class exists at compile time — `TreatWarningsAsErrors` is enabled and a missing icon will fail the build. Pick whichever valid icon best conveys "compaction/compression". Visible to all authenticated users (page is viewable by ReadOnly; trigger action is operator-gated). Add "Compaction" and "Compaction Manager" entries to `CommandPaletteCatalog.cs` navigating to `/compaction`.

11. **Data loading and refresh** — Initial data loads in `OnInitializedAsync` via `AdminCompactionApiClient`. Manual refresh via "Refresh" button. Error state shows `IssueBanner` with "Unable to load compaction jobs" and retry button. `IAsyncDisposable` for cleanup.

12. **Admin role enforcement** — The `/compaction` page is visible to all authenticated users (`ReadOnly` minimum) for viewing compaction history. All write operations (trigger compaction) require `Operator` role minimum. Trigger button is hidden for `ReadOnly` users via `AuthorizedView`. API calls for trigger use `Operator`-level endpoint.

13. **Accessibility** — Page heading `<h1>Compaction</h1>` is the first focusable element. All dialogs use `FluentDialog` with proper `aria-label`. Form fields have associated labels. Stat cards follow existing `StatCard` accessibility pattern. Data grid follows `FluentDataGrid` built-in accessibility. Status badges have `aria-label` attributes (e.g., `aria-label="Status: Completed"`).

## Tasks / Subtasks

- [x] **Task 1: Create CompactionJob DTO and CompactionJobStatus enum** (AC: 1, 6, 7)
  - [x] 1.1 Create `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/CompactionJobStatus.cs` — enum with values: `Pending`, `Running`, `Completed`, `Failed`. Follow same file pattern as existing enums in Admin.Abstractions (file-scoped namespace, XML doc comments).
  - [x] 1.2 Create `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/CompactionJob.cs` — record:
    ```csharp
    /// <summary>
    /// Represents a compaction job with its status and metrics.
    /// </summary>
    /// <param name="OperationId">Unique identifier from the trigger operation.</param>
    /// <param name="TenantId">Tenant that was compacted.</param>
    /// <param name="Domain">Domain scope, or null for all domains in tenant.</param>
    /// <param name="Status">Current job status.</param>
    /// <param name="StartedAtUtc">When the compaction was triggered.</param>
    /// <param name="CompletedAtUtc">When the compaction finished, or null if still running.</param>
    /// <param name="EventsCompacted">Number of events processed, or null if not yet available.</param>
    /// <param name="SpaceReclaimedBytes">Bytes reclaimed, or null if backend doesn't support (NFR44).</param>
    /// <param name="ErrorMessage">Error details when status is Failed, otherwise null.</param>
    public record CompactionJob(
        string OperationId,
        string TenantId,
        string? Domain,
        CompactionJobStatus Status,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        long? EventsCompacted,
        long? SpaceReclaimedBytes,
        string? ErrorMessage);
    ```
  - [x] 1.3 **Checkpoint**: DTOs compile, follow existing record patterns (see `SnapshotPolicy.cs`, `StorageOverview.cs`).

- [x] **Task 2: Add compaction query endpoint to server** (AC: 1, 2, 11)
  - [x] 2.1 Add `GetCompactionJobsAsync(string? tenantId, CancellationToken ct)` to `IStorageQueryService` interface in `src/Hexalith.EventStore.Admin.Abstractions/Services/IStorageQueryService.cs`, returning `Task<IReadOnlyList<CompactionJob>>`.
  - [x] 2.2 Implement `GetCompactionJobsAsync` in `DaprStorageQueryService`. Read from admin index key `admin:storage-compaction-jobs:{scope}` (where scope is tenantId or "all"). Follow the exact same pattern as `GetSnapshotPoliciesAsync` — read from DAPR state store, deserialize, handle missing index gracefully (return empty list with logging). The key pattern follows the existing convention: `admin:storage-compaction-jobs:all` for unfiltered, `admin:storage-compaction-jobs:{tenantId}` for tenant-scoped.
  - [x] 2.3 Add `GetCompactionJobs` action to `AdminStorageController`:
    ```csharp
    /// <summary>
    /// Gets compaction job history with optional tenant filter.
    /// </summary>
    [HttpGet("compaction-jobs")]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ProducesResponseType(typeof(IReadOnlyList<CompactionJob>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetCompactionJobs(
        [FromQuery] string? tenantId = null,
        CancellationToken ct = default)
    ```
    Follow the same pattern as `GetSnapshotPolicies` — delegate to `IStorageQueryService.GetCompactionJobsAsync`, return `Ok(result)`. Apply `AdminTenantAuthorizationFilter` when tenantId is provided.
  - [x] 2.4 **Checkpoint**: GET endpoint compiles, returns empty list when no compaction data exists, follows existing controller patterns.

- [x] **Task 3: Create AdminCompactionApiClient** (AC: 1, 4, 11)
  - [x] 3.1 Create `src/Hexalith.EventStore.Admin.UI/Services/AdminCompactionApiClient.cs` following `AdminSnapshotApiClient` pattern exactly: constructor takes `IHttpClientFactory` + `ILogger<AdminCompactionApiClient>`, uses named client `"AdminApi"`. Mark all public methods `virtual` for NSubstitute mocking.
  - [x] 3.2 Add method `GetCompactionJobsAsync(string? tenantId, CancellationToken ct)` → `HttpClient.GetAsync("api/v1/admin/storage/compaction-jobs?tenantId={id}")`, returns `IReadOnlyList<CompactionJob>`. Return empty list on error.
  - [x] 3.3 Add method `TriggerCompactionAsync(string tenantId, string? domain, CancellationToken ct)` → `HttpClient.PostAsync("api/v1/admin/storage/{Uri.EscapeDataString(tenantId)}/compact?domain={Uri.EscapeDataString(domain)}", null)` (empty body, domain via query param). **URL-encode both `tenantId` and `domain`** via `Uri.EscapeDataString()` before interpolating into the URL. Returns `AdminOperationResult?`. Expect 202 Accepted (not 200) — parse the response body as `AdminOperationResult`. Throw `ServiceUnavailableException` on non-auth errors.
  - [x] 3.4 Error handling: copy `HandleErrorStatus` from `AdminSnapshotApiClient` (maps 401 → `UnauthorizedAccessException`, 403 → `ForbiddenAccessException`, 503 → `ServiceUnavailableException`).
  - [x] 3.5 Register `AdminCompactionApiClient` as scoped in `Program.cs`: `builder.Services.AddScoped<AdminCompactionApiClient>();`
  - [x] 3.6 **Checkpoint**: Client builds, all methods callable, errors handled gracefully.

- [x] **Task 4: Create Compaction.razor page** (AC: 1, 2, 3, 4, 5, 6, 7, 8, 11, 12, 13)
  - [x] 4.1 Create `src/Hexalith.EventStore.Admin.UI/Pages/Compaction.razor` with `@page "/compaction"`. Inject: `AdminCompactionApiClient`, `NavigationManager`, `IToastService`. Implement `IAsyncDisposable`.
  - [x] 4.2 Implement `OnInitializedAsync`: parse URL tenant filter, load jobs via `GetCompactionJobsAsync`.
  - [x] 4.3 Render 3 stat cards in `FluentGrid` (xs=6, sm=6, md=4): Active Jobs (with Warning severity when > 0), Completed (30d), Space Reclaimed. Show `SkeletonCard` during loading.
  - [x] 4.4 Render tenant filter `FluentTextField` with 300ms debounce Timer. On filter change: update URL, reload data. Use `InvokeAsync(() => { ... })` in Timer callback for Blazor Server threading safety.
  - [x] 4.5 Render `FluentDataGrid` with job list. Columns: Tenant ID (monospace), Domain (or "All"), Status (badge), Started (relative time), Duration, Events Compacted (right-aligned), Space Reclaimed (FormatBytes), Actions. Default sort: Started desc. Clicking a failed row toggles error detail expansion.
  - [x] 4.6 Implement `EmptyState` when no jobs match the current filter.
  - [x] 4.7 Add "Trigger Compaction" button in page header, gated by `AuthorizedView MinimumRole="AdminRole.Operator"`. On click: open trigger dialog.
  - [x] 4.8 Implement trigger compaction `FluentDialog` (single-step, matching Snapshots.razor delete dialog pattern). Title: "Trigger Compaction". Fields: Tenant ID (required), Domain (optional). Below fields: dynamic confirmation text "This will compact event streams for tenant {tenantId}..." that updates as user types, plus warning banner about resource intensity. Primary button "Start Compaction" disabled when: (a) Tenant ID is empty, OR (b) the loaded job list contains a Pending/Running job for the same tenant — in case (b) show inline text "Compaction already in progress for this tenant." below the Tenant ID field. On click: call `TriggerCompactionAsync`. On success: close dialog, toast "Compaction started for {tenantId}. Refresh to check status.", reload. On failure: error toast, keep dialog open.
  - [x] 4.9 Implement status badges: Pending (blue), Running (orange, animated pulse), Completed (green), Failed (red). Each badge has `aria-label="Status: {status}"`.
  - [x] 4.10 Implement `FormatDuration` helper (private method or extend `TimeFormatHelper`): returns "Xm Ys" for completed jobs, "Running..." for active, "—" for pending.
  - [x] 4.11 Implement `FormatBytes` helper: reuse from `Storage.razor` if already shared, otherwise create in `TimeFormatHelper.cs` (which is already a shared helper from story 16-2). Format: "1.2 GB", "456 MB", "12 KB". Show "N/A" with tooltip when null.
  - [x] 4.12 Add manual "Refresh" button, `IssueBanner` for error state, `IAsyncDisposable` for Timer + CancellationTokenSource cleanup.
  - [x] 4.13 Add URL state management: `ReadUrlParameters()` on init, `UpdateUrl()` on filter change with `replace: true`.
  - [x] 4.14 **Checkpoint**: Page loads, stat cards show, grid renders, trigger dialog works with confirmation step, role enforcement active, URL state persists.

- [x] **Task 5: bUnit and unit tests** (AC: 1-13)
  - **Mock dependencies**: extend `AdminUITestContext`, mock `AdminCompactionApiClient` using `Substitute.For<AdminCompactionApiClient>(...)` with `IHttpClientFactory` + `NullLogger<AdminCompactionApiClient>.Instance`.
  - **Culture sensitivity**: Any test asserting formatted numbers must use culture-invariant assertions (lesson from story 16-1 with French locale).
  - **Merge-blocking tests**:
  - [x] 5.1 Test `Compaction` page renders 3 stat cards with correct values from job list (AC: 2)
  - [x] 5.2 Test `Compaction` page shows `SkeletonCard` during loading state (AC: 2)
  - [x] 5.3 Test job grid renders all jobs with correct columns (AC: 1)
  - [x] 5.4 Test `EmptyState` shown when no jobs exist (AC: 1)
  - [x] 5.5 Test `IssueBanner` shown when API returns error (AC: 11)
  - [x] 5.6 Test compaction page has `<h1>Compaction</h1>` heading (AC: 13)
  - [x] 5.7 Test "Trigger Compaction" button hidden for ReadOnly users (AC: 12)
  - [x] 5.8 Test "Trigger Compaction" button visible for Operator users (AC: 12)
  - [x] 5.9 Test trigger dialog calls `TriggerCompactionAsync` on confirm, reloads list, and shows success toast (AC: 4, 5) — **trigger happy path**
  - [x] 5.10 Test trigger dialog shows error toast when API returns failure (AC: 4) — **trigger error path**
  - [x] 5.11 Test status badges render correct appearance per job status (AC: 6)
  - **Recommended tests**:
  - [x] 5.12 Test URL parameters read on page initialization (AC: 8)
  - [x] 5.13 Test trigger dialog renders with correct form fields and warning banner (AC: 4, 5)
  - [x] 5.14 Test "Start Compaction" button disabled when Tenant ID is empty (AC: 4)
  - [x] 5.15 Test stat card "Active Jobs" shows Warning severity when count > 0 (AC: 2)
  - [x] 5.16 Test stat card "Space Reclaimed" shows "N/A" when no completed jobs have space data (AC: 2, 7)
  - [x] 5.17 Test failed job row expands to show error message on click (AC: 6)
  - [x] 5.18 Test job list filters by tenant when tenant filter is applied (AC: 3)
  - [x] 5.19 Test "Domain" column shows "All" when domain is null (AC: 1)
  - [x] 5.20 Test `TimeFormatHelper.FormatBytes` boundary cases: null → "N/A", 0 → "0 B", 1023 → "1,023 B", 1024 → "1.0 KB", 1_048_576 → "1.0 MB", 1_073_741_824 → "1.0 GB" (AC: 7) — create `tests/Hexalith.EventStore.Admin.UI.Tests/Components/TimeFormatHelperTests.cs` if it doesn't exist

- [x] **Task 6: Breadcrumb, NavMenu, Command Palette, and CSS** (AC: 9, 10, 13)
  - [x] 6.1 Update `Breadcrumb.razor` route label dictionary: add `"compaction" -> "Compaction"`.
  - [x] 6.2 Update `NavMenu.razor`: add Compaction `FluentNavLink` after the Snapshots link. Use `Icons.Regular.Size20.ArrowMinimize` (or `Compress`/`BoxArrowDown` if it doesn't compile — verify icon class exists at build time).
  - [x] 6.3 Update `CommandPaletteCatalog.cs`: add entries `("Actions", "Compaction", "/compaction")` and `("Compaction", "Compaction Manager", "/compaction")`.
  - [x] 6.4 Add CSS styles in `wwwroot/css/app.css`: `.compaction-status-running` with pulse animation for Running badge, `.compaction-error-detail` for expandable error row (monospace, muted background).
  - [x] 6.5 Add a "Run Compaction" cross-link on the Storage page's tenant breakdown grid: when a tenant row has >50,000 total events, render a `FluentAnchor` in the row's actions column linking to `/compaction?tenant={tenantId}`. This mirrors the snapshot risk banner pattern from story 16-1 — proactive DBA guidance from the Storage page directly into the Compaction trigger flow.
  - [x] 6.6 **Checkpoint**: Compaction page accessible from sidebar, breadcrumb shows "Home / Compaction", Ctrl+K "Compaction" navigates correctly, Running badges pulse, error details styled.

## Dev Notes

### Architecture Compliance

- **ADR-P4**: Admin.UI communicates exclusively via HTTP REST API to Admin.Server. The `AdminCompactionApiClient` calls `api/v1/admin/storage/*` endpoints — no direct DAPR access. The `TriggerCompactionAsync` write operation is delegated through Admin.Server which itself delegates to CommandApi via DAPR service invocation.
- **NFR41**: Initial render ≤ 2 seconds. Compaction job list is typically small (< 50 entries), so no pagination needed for v1.
- **NFR44**: Space reclaimed (`SpaceReclaimedBytes`) may be null when the DAPR state store backend does not support size reporting. Handle gracefully with "N/A" display.
- **NFR46**: Operator role required for trigger compaction. ReadOnly users can view compaction history but cannot trigger.
- **FR76**: This story covers the "trigger compaction" portion of FR76. Storage growth analysis is story 16-1 (done). Snapshot management is story 16-2 (done). Backup operations are story 16-4.

### Scope — Compaction Trigger + Job History

This story includes:
- Viewing compaction job history and status (all users)
- Triggering compaction per tenant with optional domain scope (Operator+)
- Status display with space reclaimed metrics
- Summary statistics (active jobs, completed count, total space reclaimed)

This story does NOT include:
- Scheduled/recurring compaction (future enhancement — Maria's "weekend maintenance window" scenario is manual trigger for v1)
- Compaction policy configuration (future — unlike snapshot policies, compaction policies are not yet modeled)
- Event archival or tiered storage (architecture defers to v3)
- Per-aggregate compaction granularity (compaction is per-tenant or per-tenant+domain)

### Compaction Endpoint — Already Exists on Server

The `AdminStorageController` already has the compact trigger endpoint. **Do NOT recreate it.** Use the existing:

```
POST api/v1/admin/storage/{tenantId}/compact?domain={domain}
```
- Returns **202 Accepted** (not 200) — this is an async operation via `MapAsyncOperationResult()`
- Response body is `AdminOperationResult` with `OperationId` for tracking
- Requires `Operator` auth policy
- Has `AdminTenantAuthorizationFilter`

The **new** work is:
1. `CompactionJob` DTO + `CompactionJobStatus` enum (Task 1)
2. `GetCompactionJobsAsync` query method + GET endpoint (Task 2)
3. `AdminCompactionApiClient` HTTP client (Task 3)
4. `Compaction.razor` UI page (Task 4)

### Compaction Job State Persistence — Expected Empty State

The `GetCompactionJobsAsync` query reads from admin index key `admin:storage-compaction-jobs:{scope}` in the DAPR state store. **This index is populated by CommandApi's compaction handler** when it processes a `POST /{tenantId}/compact` request — not by Admin.Server directly (ADR-P4: Admin.Server has read-only access to the event store state store).

**If no compaction has ever been triggered**, the admin index key will not exist, and `DaprStorageQueryService` will return an empty list. The page will show `EmptyState` — this is the correct and expected behavior, NOT a bug.

**After triggering compaction** via the UI, the job should appear on the next manual refresh (assuming CommandApi writes the job state back to the admin index). If the job does not appear after triggering, it means CommandApi's compaction handler does not yet write to the admin index — in that case the page still functions correctly as a trigger-only UI with EmptyState display. The dev agent should NOT attempt to fix CommandApi's write-back behavior; that is outside scope.

**Verification**: After implementing, trigger a compaction via the UI and click Refresh. If the job appears in the grid, the full flow works. If the grid stays empty, the trigger still works (check server logs for 202 response) — the job history feature will require a separate CommandApi update to populate the admin index.

### Compaction Metrics — UI Displays What Backend Reports

The architecture notes "Event stream compaction/archival" as v2/v3 scope. The `POST /{tenantId}/compact` endpoint exists but the backend's actual compaction depth may vary — from metadata reorganization to full pre-snapshot event removal. **The UI displays whatever metrics the backend reports.** If the backend returns zero events compacted and zero space reclaimed, this is correct behavior — the UI does not generate, estimate, or fake metrics. The page is valid as a trigger + monitoring surface regardless of backend compaction depth. The `CompactionJob.EventsCompacted` and `SpaceReclaimedBytes` fields are nullable precisely for this reason.

### Existing Code to Reuse (DO NOT Recreate)

| What | Where | How |
|------|-------|-----|
| `AdminOperationResult` DTO | `Admin.Abstractions/Models/Common/AdminOperationResult.cs` | USE — Success, OperationId, Message, ErrorCode for trigger response |
| `AdminRole` enum | `Admin.Abstractions/Models/Common/AdminRole.cs` | USE — ReadOnly, Operator, Admin for role gating |
| `IStorageQueryService` | `Admin.Abstractions/Services/IStorageQueryService.cs` | MODIFY — add `GetCompactionJobsAsync` |
| `IStorageCommandService` | `Admin.Abstractions/Services/IStorageCommandService.cs` | REFERENCE — `TriggerCompactionAsync` already defined |
| `AdminStorageController` | `Admin.Server/Controllers/AdminStorageController.cs` | MODIFY — add GET compaction-jobs endpoint |
| `DaprStorageQueryService` | `Admin.Server/Services/DaprStorageQueryService.cs` | MODIFY — add `GetCompactionJobsAsync` implementation |
| `AdminSnapshotApiClient` | `Admin.UI/Services/AdminSnapshotApiClient.cs` | PATTERN MODEL — follow exact same constructor, HttpClient, error handling, `HandleErrorStatus` pattern |
| `Snapshots.razor` | `Admin.UI/Pages/Snapshots.razor` | PATTERN MODEL — page with stat cards, grid, operator-gated action buttons, dialogs, URL state, debounce |
| `StatCard` component | `Admin.UI/Components/Shared/StatCard.razor` | USE — Label, Value, Severity, Title |
| `SkeletonCard` component | `Admin.UI/Components/Shared/SkeletonCard.razor` | USE — loading placeholder |
| `IssueBanner` component | `Admin.UI/Components/Shared/IssueBanner.razor` | USE — error/warning state |
| `EmptyState` component | `Admin.UI/Components/Shared/EmptyState.razor` | USE — no data fallback |
| `AuthorizedView` component | `Admin.UI/Components/Shared/AuthorizedView.razor` | USE — role-gated rendering with `MinimumRole` |
| `TimeFormatHelper` | `Admin.UI/Components/Shared/TimeFormatHelper.cs` | USE — `FormatRelativeTime` for Started column |
| `AdminUITestContext` | `Admin.UI.Tests/AdminUITestContext.cs` | USE — base test class |
| `Storage.razor` | `Admin.UI/Pages/Storage.razor` | REFERENCE — FormatBytes, URL state, debounce timer patterns |
| `HandleErrorStatus` method | `AdminSnapshotApiClient.cs` | COPY — same HTTP status → typed exception mapping |

### AdminCompactionApiClient — Exact API Endpoints

| Method | HTTP | URL | Params | Returns | Status |
|--------|------|-----|--------|---------|--------|
| GetCompactionJobsAsync | GET | `api/v1/admin/storage/compaction-jobs` | `?tenantId=` | `IReadOnlyList<CompactionJob>` | 200 OK |
| TriggerCompactionAsync | POST | `api/v1/admin/storage/{tenantId}/compact` | `?domain=` | `AdminOperationResult` | **202 Accepted** |

GET endpoint requires `ReadOnly` auth policy. POST endpoint requires `Operator` auth policy. Both endpoints have `AdminTenantAuthorizationFilter`.

**Important**: The POST compact endpoint returns 202 (not 200). The client must handle this — `response.StatusCode == HttpStatusCode.Accepted` is success, NOT `response.IsSuccessStatusCode` alone (though 202 is a success status code). Parse the response body as `AdminOperationResult` regardless.

### Dialog Pattern — Single-Step, Match Existing Codebase

**IMPORTANT:** Check how `Snapshots.razor` implements its dialogs before coding. Match the exact same pattern — `FluentDialog` with `ShowAsync()`/`HideAsync()` or `IDialogService.ShowDialogAsync<T>()`, whichever the codebase uses. Do NOT mix APIs.

The trigger dialog is a **single-step dialog** (NOT multi-step). It combines the form fields, live confirmation text, and warning banner in one view — matching the Snapshots.razor delete dialog pattern. This keeps implementation simple and tests straightforward.

```csharp
// Single dialog with form + inline confirmation + warning
// "Start Compaction" button disabled until TenantId is non-empty
private bool CanTrigger => !string.IsNullOrWhiteSpace(_triggerForm?.TenantId);

private async Task OnTriggerConfirm()
{
    _isOperating = true;
    StateHasChanged();
    try
    {
        AdminOperationResult? result = await CompactionApi.TriggerCompactionAsync(
            _triggerForm!.TenantId!, _triggerForm.Domain, _loadCts?.Token ?? default);
        if (result?.Success == true)
        {
            ToastService.ShowSuccess($"Compaction started for {_triggerForm.TenantId}. Refresh to check status.");
            // Close dialog, reload job list
            await LoadDataAsync();
        }
        else
        {
            ToastService.ShowError(result?.Message ?? "Failed to trigger compaction.");
        }
    }
    catch (ServiceUnavailableException)
    {
        ToastService.ShowError("Admin service unavailable. Try again later.");
    }
    finally
    {
        _isOperating = false;
        StateHasChanged();
    }
}
```

### FormatBytes Helper — Extract to Shared Helper

`Storage.razor` and `StorageTreemap.razor` both have independent private `FormatBytes` methods (NOT yet shared). Extract `FormatBytes` as a new `public static` method in `TimeFormatHelper.cs` (the shared helper created in story 16-2). Then update `Storage.razor`, `StorageTreemap.razor`, and the new `Compaction.razor` to all use `TimeFormatHelper.FormatBytes()`. This avoids three copies of the same logic.

**Before extracting:** Diff the `FormatBytes` implementations in `Storage.razor` and `StorageTreemap.razor`. If they differ (rounding, thresholds, edge cases), use the `Storage.razor` version as canonical and align `StorageTreemap.razor` to match. **After extraction:** Run `StoragePageTests` and `StorageTreemapTests` to verify no visual regression from the refactor.

Pattern:
```csharp
public static string FormatBytes(long? bytes)
{
    if (bytes is null) return "N/A";
    double b = bytes.Value;
    return b switch
    {
        >= 1_073_741_824 => $"{b / 1_073_741_824:F1} GB",
        >= 1_048_576 => $"{b / 1_048_576:F1} MB",
        >= 1024 => $"{b / 1024:F1} KB",
        _ => $"{b:N0} B"
    };
}
```

### bUnit Test Pattern

Follow `SnapshotsPageTests` pattern from story 16-2:

```csharp
public class CompactionPageTests : AdminUITestContext
{
    private readonly AdminCompactionApiClient _mockCompactionApi;

    public CompactionPageTests()
    {
        _mockCompactionApi = Substitute.For<AdminCompactionApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminCompactionApiClient>.Instance);
        Services.AddScoped(_ => _mockCompactionApi);
    }

    [Fact]
    public void CompactionPage_RendersStatCards_WithCorrectValues()
    {
        IReadOnlyList<CompactionJob> jobs =
        [
            new("op-1", "tenant-a", null, CompactionJobStatus.Completed,
                DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-1),
                5000, 1_048_576, null),
            new("op-2", "tenant-b", "orders", CompactionJobStatus.Running,
                DateTimeOffset.UtcNow.AddMinutes(-5), null, null, null, null),
        ];
        _mockCompactionApi.GetCompactionJobsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(jobs));

        IRenderedComponent<Compaction> cut = Render<Compaction>();
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("Active Jobs"), TimeSpan.FromSeconds(5));
        cut.Markup.ShouldContain("1"); // Active Jobs (1 running)
    }
}
```

### Previous Story Intelligence (16-2 Snapshot Management)

Key learnings from story 16-2 that apply to 16-3:

- **Culture-dependent formatting in tests**: Use format-string assertions rather than hardcoded locale-dependent strings. Story 16-1 learned this with French locale producing "1,0" instead of "1.0". Apply to FormatBytes and number formatting.
- **FluentDataGrid `OnRowClick`**: Use `OnRowClick` attribute, not `RowClick`. For compaction, row click toggles error detail expansion for failed jobs.
- **Timer + InvokeAsync**: The debounce Timer callback runs on a thread pool thread. All state mutations and `StateHasChanged()` MUST be wrapped in `InvokeAsync(() => { ... })`.
- **AdminApiClient error pattern**: Throws `ServiceUnavailableException` on non-auth errors. The page catches this and shows `IssueBanner`.
- **242 Admin.UI tests pass** after story 16-2 — no regressions allowed.
- **JSInterop in tests uses `Mode = Loose`** — no explicit JS setup needed.
- **FluentUI Blazor v4 icon syntax**: `@(new Icons.Regular.Size20.IconName())`. Verify the icon class exists at compile time — try `ArrowMinimize`, `Compress`, or `BoxArrowDown` for compaction metaphor.
- **`DashboardRefreshService`**: Do NOT subscribe on this page — compaction data changes infrequently. Manual refresh only.
- **BL0005 error with `@onclick:stopPropagation`**: If adding click handlers to elements inside grid rows, wrap in `<div @onclick:stopPropagation>` to prevent conflicting with row click.
- **Dialog pattern**: Story 16-2 used `FluentDialog` with form fields and toast notifications. Follow the same pattern for the trigger compaction dialog.
- **AdminSnapshotApiClient as pattern model**: Constructor takes `IHttpClientFactory` + `ILogger<T>`, marks all public methods `virtual` for NSubstitute mocking.

### Deferred Improvements (Out of Scope — Future Stories)

- **Scheduled compaction** — Maria's Journey 8 scenario mentions "scheduling a compaction job for the weekend maintenance window." v1 supports manual trigger only. Consider adding cron-style scheduling in a future iteration.
- **Compaction policies** — Unlike snapshot policies (which have CRUD), compaction doesn't yet have configurable policies (e.g., "compact tenant X every Sunday at 2 AM"). Consider adding compaction policy configuration alongside the scheduling feature.
- **Real-time job status updates** — Currently requires manual refresh. Consider subscribing to SignalR for real-time compaction job status updates in a future iteration.
- **Per-aggregate compaction granularity** — Current API scopes to tenant or tenant+domain. Per-aggregate compaction would require API changes.
- **Compaction impact estimator** — Before triggering, show estimated time and resource impact based on stream sizes. Requires server-side analysis.

### Git Intelligence

Branch naming: `feat/story-16-3-compaction-manager`. PR workflow: feature branch → PR → merge to main. Conventional commits: `feat:` prefix.

### Project Structure Notes

Files to create:
- **CREATE**: `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/CompactionJob.cs` — DTO record
- **CREATE**: `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/CompactionJobStatus.cs` — Status enum
- **CREATE**: `src/Hexalith.EventStore.Admin.UI/Services/AdminCompactionApiClient.cs` — HTTP client for compaction endpoints
- **CREATE**: `src/Hexalith.EventStore.Admin.UI/Pages/Compaction.razor` — Compaction manager page
- **CREATE**: `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/CompactionPageTests.cs` — bUnit tests
- **CREATE**: `tests/Hexalith.EventStore.Admin.UI.Tests/Components/TimeFormatHelperTests.cs` — FormatBytes boundary tests (if not already existing)

Files to modify:
- **MODIFY**: `src/Hexalith.EventStore.Admin.Abstractions/Services/IStorageQueryService.cs` — add `GetCompactionJobsAsync`
- **MODIFY**: `src/Hexalith.EventStore.Admin.Server/Services/DaprStorageQueryService.cs` — add `GetCompactionJobsAsync` implementation
- **MODIFY**: `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStorageController.cs` — add GET compaction-jobs endpoint
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Program.cs` — register `AdminCompactionApiClient`
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor` — add Compaction nav link
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Layout/Breadcrumb.razor` — add "compaction" route label
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Components/CommandPaletteCatalog.cs` — add Compaction entries
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css` — compaction-specific styles (pulse animation, error detail)
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Components/Shared/TimeFormatHelper.cs` — add `FormatBytes` static method (extracted from Storage.razor/StorageTreemap.razor)
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Pages/Storage.razor` — replace private `FormatBytes` with `TimeFormatHelper.FormatBytes`
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Components/StorageTreemap.razor` — replace private `FormatBytes` with `TimeFormatHelper.FormatBytes`

### References

- [Source: prd.md — Journey 8: Maria, the DBA — compaction scheduling for weekend maintenance]
- [Source: prd.md — FR76: Admin tool can manage storage — trigger compaction]
- [Source: prd.md — NFR46: Admin API role-based access control — operator for snapshot/compaction]
- [Source: prd.md — NFR44: All admin data access through DAPR abstractions (state-store agnostic)]
- [Source: architecture.md — ADR-P4: Three-interface architecture, Admin.UI via HTTP to Admin.Server]
- [Source: architecture.md — NFR41: Admin Web UI render ≤ 2 seconds initial load]
- [Source: architecture.md — Event stream compaction/archival: v2/v3 scope]
- [Source: architecture.md — State store key patterns: admin:storage-compaction-jobs:{scope}]
- [Source: architecture.md — Admin.Server write operations delegated to CommandApi via DAPR service invocation]
- [Source: ux-design-specification.md — UX-DR41: Command palette for global search]
- [Source: ux-design-specification.md — UX-DR42: Deep linking for every view]
- [Source: ux-design-specification.md — Confirmation dialogs for batch/destructive operations]
- [Source: ux-design-specification.md — Destructive button styling: red text + outline]
- [Source: Admin.Abstractions/Services/IStorageCommandService.cs — TriggerCompactionAsync already defined]
- [Source: Admin.Server/Controllers/AdminStorageController.cs — POST {tenantId}/compact endpoint already defined, returns 202]
- [Source: Admin.UI/Services/AdminSnapshotApiClient.cs — HTTP client pattern to follow exactly]
- [Source: story 16-2 — Previous story learnings: Timer+InvokeAsync, error patterns, test patterns, dialog patterns]
- [Source: story 16-1 — Culture-dependent formatting, FluentDataGrid patterns, FormatBytes]
- [Source: sprint-change-proposal-2026-03-21-admin-tooling.md — Epic 16 story breakdown: 16-3 compaction manager]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Fixed culture-dependent FormatBytes to use CultureInfo.InvariantCulture for consistent output across locales (French locale uses "," not ".")
- Updated 2 existing StoragePageTests assertions that relied on locale-dependent format strings
- Dialog test selectors fixed from AriaLabel property to Markup.Contains pattern for robust FluentTextField discovery

### Code Review Fixes (post-review)

- P1: Fixed CTS race condition — separated `_triggerCts` from `_loadCts` so LoadDataAsync refresh cannot cancel in-flight trigger POST
- P2: Added `catch (OperationCanceledException)` in OnTriggerConfirm to prevent noisy unhandled exceptions on cancellation
- P3: Stat cards (ActiveJobCount, CompletedLast30Days, SpaceReclaimed) now computed from `_jobs` (global) instead of `FilteredJobs` (tenant-filtered) — AC 2 compliance
- P5: Cached `_filteredJobs` field with explicit `UpdateFilteredJobs()` method — eliminates 4-5 redundant LINQ+ToList materializations per render
- P6: Added `_disposed` guard in debounce timer callback to prevent use-after-dispose on component teardown
- P7: FormatTimeSpan guards against negative durations (clock skew) — returns em-dash instead of confusing negative values
- P8: FormatBytes guards against negative byte values — returns "N/A"
- P9: Added missing `[ProducesResponseType(403)]` on GetCompactionJobs endpoint for OpenAPI spec completeness
- P4 (Appearance.Warning for Running badge): Rejected — `Appearance.Warning` does not exist in FluentUI Blazor; `Appearance.Lightweight` + CSS override is the correct approach

### Completion Notes List

- Task 1: Created CompactionJobStatus enum (Pending/Running/Completed/Failed) and CompactionJob record DTO with all specified fields including nullable SpaceReclaimedBytes (NFR44 compliance)
- Task 2: Added GetCompactionJobsAsync to IStorageQueryService interface, DaprStorageQueryService implementation (reads from admin:storage-compaction-jobs:{scope} index), and GET compaction-jobs endpoint to AdminStorageController with ReadOnly auth policy
- Task 3: Created AdminCompactionApiClient following AdminSnapshotApiClient pattern exactly — constructor with IHttpClientFactory + ILogger, virtual methods for NSubstitute mocking, HandleErrorStatus with typed exceptions, URL-encoded parameters
- Task 4: Created full Compaction.razor page with stat cards (Active Jobs with warning severity, Completed 30d, Space Reclaimed), FluentDataGrid with sortable columns, trigger dialog with concurrent guard, status badges with aria-labels, debounced tenant filter, URL state persistence, IAsyncDisposable cleanup
- Task 5: Created 20 bUnit tests in CompactionPageTests (11 merge-blocking + 9 recommended) and 6 FormatBytes unit tests in TimeFormatHelperTests. All 258 tests pass with 0 regressions
- Task 6: Added breadcrumb "compaction" -> "Compaction", NavMenu link with ArrowMinimize icon after Snapshots, 2 CommandPalette entries, CSS animations (pulse for Running badge, monospace error detail), Storage page cross-link for tenants with >50k events
- Extracted FormatBytes from Storage.razor and StorageTreemap.razor to shared TimeFormatHelper.FormatBytes with InvariantCulture

### File List

**Created:**
- src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/CompactionJobStatus.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/CompactionJob.cs
- src/Hexalith.EventStore.Admin.UI/Services/AdminCompactionApiClient.cs
- src/Hexalith.EventStore.Admin.UI/Pages/Compaction.razor
- tests/Hexalith.EventStore.Admin.UI.Tests/Pages/CompactionPageTests.cs
- tests/Hexalith.EventStore.Admin.UI.Tests/Components/TimeFormatHelperTests.cs

**Modified:**
- src/Hexalith.EventStore.Admin.Abstractions/Services/IStorageQueryService.cs
- src/Hexalith.EventStore.Admin.Server/Services/DaprStorageQueryService.cs
- src/Hexalith.EventStore.Admin.Server/Controllers/AdminStorageController.cs
- src/Hexalith.EventStore.Admin.UI/Program.cs
- src/Hexalith.EventStore.Admin.UI/Components/Shared/TimeFormatHelper.cs
- src/Hexalith.EventStore.Admin.UI/Pages/Storage.razor
- src/Hexalith.EventStore.Admin.UI/Components/StorageTreemap.razor
- src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor
- src/Hexalith.EventStore.Admin.UI/Layout/Breadcrumb.razor
- src/Hexalith.EventStore.Admin.UI/Components/CommandPaletteCatalog.cs
- src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css
- tests/Hexalith.EventStore.Admin.UI.Tests/Pages/StoragePageTests.cs
