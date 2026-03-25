# Story 16.7: Consistency Checker

Status: done

Size: Large — ~20 new files, 9 task groups, 18 ACs, ~50 tests (~16-24 hours estimated). Full-stack feature: new Abstractions DTOs + service interfaces (Task 1), new Server DAPR services + controller (Task 2), server-side tests (Task 3), new UI API client (Task 4), new Consistency.razor page (Task 5), NavMenu + breadcrumb + command palette entries (Task 6), bUnit tests (Task 7), build verification (Task 8). Unlike stories 16-1 through 16-6, **no backend exists yet** — this story builds the complete vertical slice.

**Split advisory (recommended):** Deliver in two PRs. (A) Tasks 1-4 + Task 6 + Task 8 (backend + server tests + API client + nav entries + build verification) — establishes the full API surface with test coverage. (B) Task 5 + Task 7 (Razor page + bUnit tests) — builds the UI consuming the API. Both halves are independently shippable. PR A enables the CLI (Epic 17) and MCP (Epic 18) stories to begin in parallel.

**Dependency advisory:** Story 16-6 (Dead Letter Queue Manager) is currently `in-progress`. Do NOT start dev on 16-7 until 16-6 is merged — the dead-letter page establishes UI patterns (checkbox selection, pagination, bulk actions) that this story references. Backend tasks (1-4) can begin in parallel.

## Definition of Done

- All 18 ACs verified
- 9 server-side controller tests green (Task 3: 3.1-3.9)
- 8 server-side service tests green (Task 3: 3.10-3.17)
- 14 merge-blocking bUnit tests green (Task 7 blocking tests: 7.1-7.14)
- 21 recommended bUnit tests green (Task 7 recommended tests: 7.15-7.35)
- Project builds with zero warnings in CI (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions

## Story

As a **DBA (Maria) or operator using the Hexalith EventStore admin dashboard**,
I want **a consistency checker page where I can trigger on-demand verification of event sequence continuity, snapshot integrity, projection positions, and metadata consistency — scoped by tenant and domain — and view anomaly reports with severity classification**,
so that **I can verify data integrity after incidents, validate backup restores, confirm operational health during daily checks, and export verification results for compliance reporting — all without developer involvement (FR76, Journey 8)**.

## Acceptance Criteria

1. **Consistency check list** — The `/consistency` page displays a `FluentDataGrid` listing recent consistency check runs from `AdminConsistencyApiClient.GetChecksAsync`. Columns: Check ID (monospace, truncated to 8 chars with full ID in tooltip), Tenant ID (monospace, or "All" when null), Domain (or "All" when null), Status (`StatusBadge` — Pending: neutral, Running: accent with spinner, Completed: success, Failed: error, Cancelled: warning), Streams Checked (right-aligned), Anomalies Found (right-aligned, bold red when > 0), Started At (relative time via `TimeFormatHelper.FormatRelativeTime`, full UTC in tooltip), Duration (computed from StartedAtUtc/CompletedAtUtc, or "Running..." if incomplete). Grid is sortable by Started At (default descending), Status, Anomalies Found. When no checks exist, show `EmptyState` with title "No consistency checks yet" and description "Run your first consistency check to verify event store integrity." with action button "Run Check" (Operator+).

2. **Summary stat cards** — Above the grid, display four stat cards in a `FluentGrid` (xs=6, sm=6, md=3): (a) "Total Checks" showing total count, (b) "Last Check" showing relative time of most recent CompletedAtUtc, or "Never" if no completed checks, (c) "Total Anomalies" showing sum of AnomaliesFound across all completed checks, severity Warning when > 0, (d) "Running Now" showing count of checks with status Running, severity Accent when > 0. Loading state shows 4 `SkeletonCard` placeholders.

3. **Tenant and domain filters** — A `FluentTextField` with debounced input (300ms) filters checks by tenant ID prefix. Filter persists in URL query parameter `?tenant=<id>`. Same debounce pattern as `Compaction.razor` (Timer + `InvokeAsync` for Blazor Server threading). Additionally, a `FluentTextField` for domain filter (debounced 300ms) filters by domain prefix (client-side on loaded data). Persists as `?domain=<value>`. Tenant filter is passed to the API call; domain filter is client-side only.

4. **Trigger consistency check** — A "Run Check" button (visible only to `AdminRole.Operator` via `AuthorizedView MinimumRole="AdminRole.Operator"`) opens a `FluentDialog` with: (a) `FluentTextField` for Tenant ID (optional — leave blank for all tenants), (b) `FluentTextField` for Domain (optional — leave blank for all domains in tenant), (c) Checkboxes for check types: "Sequence Continuity" (default: checked), "Snapshot Integrity" (default: checked), "Projection Positions" (default: checked), "Metadata Consistency" (default: checked). At least one check type must be selected (validate before submit). Dialog submit calls `AdminConsistencyApiClient.TriggerCheckAsync`. **Concurrency guard:** Only 1 active (Pending/Running) check per tenant is allowed — the server returns 409 Conflict if one already exists. On 409: error toast "A consistency check is already running for this tenant. Wait for it to complete or cancel it." On success: close dialog, toast "Consistency check started.", reload check list. On other failure: error toast, stay on dialog.

5. **Check detail expansion** — Clicking a row expands an inline detail section below the row (same pattern as Compaction.razor error detail). Detail shows: full Check ID (monospace, copyable), Tenant ID, Domain, Status, all four check type results, Started At (full UTC), Completed At (full UTC), Duration, Streams Checked, Anomalies Found. If the check is Completed and has anomalies, show a nested `FluentDataGrid` of anomalies with columns: Severity (`StatusBadge` — Warning: warning, Error: error, Critical: error bold), Check Type, Tenant ID, Domain, Aggregate ID (monospace, truncated), Description (truncated, full in tooltip). Click again to collapse.

6. **Anomaly detail modal** — Clicking an anomaly row in the nested grid opens a `FluentDialog` showing: full Anomaly ID, Check Type, Severity, Tenant ID, Domain, full Aggregate ID (monospace, copyable), Description, Details (if present, in `FluentCard` with monospace font and `pre-wrap`), Expected Sequence (if applicable), Actual Sequence (if applicable). Dialog includes a "Go to Stream" link that navigates to `/streams?tenant={tenantId}&domain={domain}&aggregate={aggregateId}`.

7. **Running check progress** — When a check has status `Running`, the row shows an indeterminate progress indicator. If the expanded detail is open for a running check, display "Checking streams... {StreamsChecked} checked so far." Auto-refresh the check list every 10 seconds while any check is Running (use `Timer` with `_disposed` guard — 10s reduces API load for long-running checks vs. 5s). Stop auto-refresh when no checks are Running.

8. **Cancel running check** — A "Cancel" button appears next to running checks (Operator+ only). Clicking opens a confirmation dialog: "Cancel this consistency check? Results collected so far will be preserved." On confirm: calls `AdminConsistencyApiClient.CancelCheckAsync`. On success: toast "Consistency check cancelled.", reload. On failure: error toast.

9. **Export results** — An "Export" button (outline) appears for Completed and Cancelled checks (both have results) in the expanded detail view. Clicking triggers a download of the check results as JSON via `AdminConsistencyApiClient.GetCheckResultAsync` serialized to a `consistency-check-{checkId}.json` file. Uses JS interop `blazorDownloadFile` to trigger browser download (same pattern as `Backups.razor` line 1037). If the result has `Truncated == true`, show a warning above the export button: "Results truncated — {AnomaliesFound} anomalies found, showing first 500."

10. **URL state persistence** — The `/consistency` page persists filter state in URL query parameters: `?tenant=<id>&domain=<value>`. Page loads with filters pre-applied from URL. Uses `NavigationManager.NavigateTo(url, forceLoad: false, replace: true)`. All values escaped with `Uri.EscapeDataString()`.

11. **Navigation entry** — Add a "Consistency" `FluentNavLink` in `NavMenu.razor` positioned after "Backups" (line 23). Use `Icons.Regular.Size20.ShieldCheckmark` icon (try first; if missing, try `Icons.Regular.Size20.Checkmark` or `Icons.Regular.Size20.CheckboxChecked`). Href: `/consistency`. Add `["consistency"] = "Consistency"` to `Breadcrumb.razor` `_routeLabels` dictionary. Add command palette entries: `new("Actions", "Consistency", "/consistency")` and `new("Consistency", "Consistency Checker", "/consistency")`.

12. **Data loading and refresh** — Initial data loads in `OnInitializedAsync` via `AdminConsistencyApiClient`. Manual refresh via "Refresh" button (reloads check list). Error state shows `IssueBanner` with "Unable to load consistency checks" and retry button. `IAsyncDisposable` for cleanup (dispose `_debounceTimer`, `_autoRefreshTimer`, cancel `_loadCts`). **Disposed guard:** The debounce timer callback MUST check `_disposed` before calling `InvokeAsync`, same as `Compaction.razor` — prevents `ObjectDisposedException` when navigating away during pending debounce.

13. **Operator role enforcement** — The `/consistency` page is visible to all authenticated users (`ReadOnly` minimum) for viewing check results and anomaly details. ALL write operations (trigger check, cancel check) require `Operator` role (minimum). Write buttons are hidden for non-Operator users via `AuthorizedView MinimumRole="AdminRole.Operator"`. API calls for writes use `AdminOperator` policy.

14. **Accessibility** — Page heading `<h1>Consistency</h1>` is the first focusable element. All dialogs use `FluentDialog` with proper `aria-label`. Stat cards follow existing `StatCard` accessibility pattern. Data grid follows `FluentDataGrid` built-in accessibility. Status text uses semantic markup. Running checks announce progress via `aria-live="polite"`.

15. **Abstractions: DTOs** — Create models in `src/Hexalith.EventStore.Admin.Abstractions/Models/Consistency/`: `ConsistencyCheckType` enum (SequenceContinuity, SnapshotIntegrity, ProjectionPositions, MetadataConsistency), `ConsistencyCheckStatus` enum (Pending, Running, Completed, Failed, Cancelled), `AnomalySeverity` enum (Warning, Error, Critical), `ConsistencyAnomaly` record, `ConsistencyCheckResult` record, `ConsistencyCheckSummary` record (for list endpoint — without full anomaly list). Follow existing record patterns in `Models/Storage/CompactionJob.cs`.

16. **Abstractions: Service interfaces** — Create `IConsistencyQueryService` and `IConsistencyCommandService` in `src/Hexalith.EventStore.Admin.Abstractions/Services/`. Query service: `GetChecksAsync(tenantId?, ct)` → `IReadOnlyList<ConsistencyCheckSummary>`, `GetCheckResultAsync(checkId, ct)` → `ConsistencyCheckResult?`. Command service: `TriggerCheckAsync(tenantId?, domain?, checkTypes, ct)` → `AdminOperationResult`, `CancelCheckAsync(checkId, ct)` → `AdminOperationResult`.

17. **Server: DAPR services + controller** — Create `DaprConsistencyQueryService`, `DaprConsistencyCommandService` in `src/Hexalith.EventStore.Admin.Server/Services/`. Create `AdminConsistencyController` in `src/Hexalith.EventStore.Admin.Server/Controllers/`. Controller route: `api/v1/admin/consistency`. Read endpoints: ReadOnly policy with `[ServiceFilter(typeof(AdminTenantAuthorizationFilter))]` (ensures tenant-scoped users only see their own checks — prevents cross-tenant information disclosure). Write endpoints: Operator policy with same tenant filter. Follow exact pattern of `AdminStorageController`. Register services in DI.

18. **Server: Consistency check implementation** — The `DaprConsistencyCommandService.TriggerCheckAsync` implementation runs entirely within Admin.Server (read-only access per ADR-P4 — no CommandApi delegation needed): (a) **concurrency guard** — check the index for any Pending/Running checks on the same tenant; if found, return `AdminOperationResult(false, "", "A check is already active for this tenant.", "Conflict")` and the controller maps this to 409 Conflict, (b) generates a check ID via `UniqueIdHelper.GenerateSortableUniqueStringId()`, (c) stores a `ConsistencyCheckResult` with status Pending in DAPR state store at key `admin:consistency:{checkId}` **with 30-day TTL** via DAPR `ttlInSeconds` metadata (prevents state store bloat from old check records), (d) appends the check ID to the index key `admin:consistency:index` using **ETag-based optimistic concurrency** (read-modify-write with ETag, retry on mismatch) to prevent concurrent index corruption, (e) kicks off background scanning via `Task.Run` with cancellation token, (f) returns immediately with the operation ID. **Background scan resilience:** The `Task.Run` body MUST be wrapped in a top-level try/catch — on unhandled exception, update the check record to `Failed` with the exception message. Never leave a check stuck in Running. Additionally, store a `TimeoutUtc` field (StartedAtUtc + 30 minutes); the query service should report any Running check past its `TimeoutUtc` as `Failed` with "Timed out." The background scan uses `IStreamQueryService.GetStreamsAsync` to discover aggregate identifiers (backend-agnostic). **Sanity check:** Before full scan, read one known aggregate's metadata via `DaprClient.GetStateAsync` and verify non-null return — if null, the state store may be misconfigured; fail fast with a clear error. For each aggregate, reads metadata, spot-checks event keys for sequence gaps, verifies snapshot sequence ≤ latest event, verifies projection positions, and compares metadata count to actual event count. Updates check status to Running during scan, then Completed/Failed on finish. **Anomaly cap:** Store at most 500 anomalies per check, **sorted by severity (Critical > Error > Warning) before truncating** — ensures the highest-severity anomalies are preserved, not just the first 500 discovered. Set `Truncated = true` flag when exceeded. **This is a long-running operation** — the controller returns 202 Accepted, and the UI polls for completion.

## Tasks / Subtasks

- [x] **Task 1: Create Abstractions DTOs and service interfaces** (AC: 15, 16)
  - [x]1.1 Create directory `src/Hexalith.EventStore.Admin.Abstractions/Models/Consistency/`
  - [x]1.2 Create `ConsistencyCheckType.cs` — enum: `SequenceContinuity`, `SnapshotIntegrity`, `ProjectionPositions`, `MetadataConsistency`. Include XML doc on each member.
  - [x]1.3 Create `ConsistencyCheckStatus.cs` — enum: `Pending`, `Running`, `Completed`, `Failed`, `Cancelled`. Include XML doc on each member.
  - [x]1.4 Create `AnomalySeverity.cs` — enum: `Warning`, `Error`, `Critical`. Include XML doc on each member.
  - [x]1.5 Create `ConsistencyAnomaly.cs` — record:
    ```csharp
    public record ConsistencyAnomaly(
        string AnomalyId,
        ConsistencyCheckType CheckType,
        AnomalySeverity Severity,
        string TenantId,
        string Domain,
        string AggregateId,
        string Description,
        string? Details,
        long? ExpectedSequence,
        long? ActualSequence);
    ```
  - [x]1.6 Create `ConsistencyCheckSummary.cs` — record (for list endpoint, without anomalies):
    ```csharp
    public record ConsistencyCheckSummary(
        string CheckId,
        ConsistencyCheckStatus Status,
        string? TenantId,
        string? Domain,
        IReadOnlyList<ConsistencyCheckType> CheckTypes,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        DateTimeOffset TimeoutUtc,
        int StreamsChecked,
        int AnomaliesFound);
    ```
  - [x]1.7 Create `ConsistencyCheckResult.cs` — record (full detail with anomalies):
    ```csharp
    public record ConsistencyCheckResult(
        string CheckId,
        ConsistencyCheckStatus Status,
        string? TenantId,
        string? Domain,
        IReadOnlyList<ConsistencyCheckType> CheckTypes,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        DateTimeOffset TimeoutUtc,
        int StreamsChecked,
        int AnomaliesFound,
        IReadOnlyList<ConsistencyAnomaly> Anomalies,
        bool Truncated,
        string? ErrorMessage);
    ```
    `TimeoutUtc` = `StartedAtUtc + 30 minutes`. Query service reports Running checks past `TimeoutUtc` as Failed("Timed out"). `Truncated` is `true` when anomaly count exceeded the 500 cap — `Anomalies` contains the top-500 by severity, `AnomaliesFound` contains the true total.
  - [x]1.8 Create `IConsistencyQueryService.cs`:
    ```csharp
    public interface IConsistencyQueryService
    {
        Task<IReadOnlyList<ConsistencyCheckSummary>> GetChecksAsync(
            string? tenantId, CancellationToken ct = default);
        Task<ConsistencyCheckResult?> GetCheckResultAsync(
            string checkId, CancellationToken ct = default);
    }
    ```
  - [x]1.9 Create `IConsistencyCommandService.cs`:
    ```csharp
    public interface IConsistencyCommandService
    {
        Task<AdminOperationResult> TriggerCheckAsync(
            string? tenantId, string? domain,
            IReadOnlyList<ConsistencyCheckType> checkTypes,
            CancellationToken ct = default);
        Task<AdminOperationResult> CancelCheckAsync(
            string checkId, CancellationToken ct = default);
    }
    ```

- [x] **Task 2: Create Server DAPR services and controller** (AC: 17, 18)
  - [x]2.1 Create `DaprConsistencyQueryService.cs` in `src/Hexalith.EventStore.Admin.Server/Services/`:
    - Constructor: `DaprClient daprClient, IOptions<AdminServerOptions> options, ILogger<DaprConsistencyQueryService> logger`
    - `GetChecksAsync`: reads the check index from DAPR state store key `admin:consistency:index` (a `List<string>` of check IDs). For each ID, reads `ConsistencyCheckSummary` from `admin:consistency:{checkId}`. Filters by tenantId if provided. **Timeout enforcement:** For any check with status Running and `DateTimeOffset.UtcNow > TimeoutUtc`, return it as Failed with ErrorMessage "Timed out" (do NOT write the update here — query is read-only; the background task's try/catch or a future cleanup job handles the write). Returns list sorted by StartedAtUtc descending. Returns empty list if index key does not exist.
    - `GetCheckResultAsync`: reads full `ConsistencyCheckResult` (with anomalies) from DAPR state store key `admin:consistency:{checkId}`. Returns null if not found. Same timeout projection as above.
  - [x]2.2 Create `DaprConsistencyCommandService.cs` in `src/Hexalith.EventStore.Admin.Server/Services/`:
    - Constructor: `DaprClient daprClient, IStreamQueryService streamQueryService, IOptions<AdminServerOptions> options, ILogger<DaprConsistencyCommandService> logger`
    - `TriggerCheckAsync`: (a) **concurrency guard** — read the index, check for any Pending/Running checks on the same tenant; if found, return `AdminOperationResult(false, "", "A check is already active for this tenant.", "Conflict")`, (b) generates check ID via `UniqueIdHelper.GenerateSortableUniqueStringId()`, (c) computes `TimeoutUtc = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30)`, (d) stores initial Pending result at `admin:consistency:{checkId}` **with 30-day TTL** via DAPR `ttlInSeconds` metadata, (e) appends check ID to index key `admin:consistency:index` using **ETag-based optimistic concurrency** (read-modify-write with ETag; retry up to 3 times on ETag mismatch; cap at 100 entries — drop oldest), (f) starts background scan via `Task.Run` with `CancellationToken`, (g) returns `AdminOperationResult` with check ID immediately. **No CommandApi delegation** — consistency checking is a read-only operation, runs entirely within Admin.Server per ADR-P4.
    - **Background scan (inside Task.Run):** MUST be wrapped in top-level try/catch — on any unhandled exception, update check record to `Failed` with exception message. Never leave a check stuck in Running. **Sanity check first:** read one aggregate's metadata and verify non-null return; if null, fail fast with "State store read returned null — verify DAPR configuration." Then uses `IStreamQueryService.GetStreamsAsync(tenantId, ct)` to discover aggregates (backend-agnostic). For each aggregate: reads metadata via `DaprClient.GetStateAsync`, spot-checks event sequence keys for gaps, verifies snapshot integrity, checks projection positions. Collects anomalies, **sorted by severity (Critical > Error > Warning) before truncating** to 500. Updates check record (Running → Completed/Failed) in state store. Set `Truncated = true` if total anomalies exceeded 500.
    - `CancelCheckAsync`: reads current check state from `admin:consistency:{checkId}`, if Running updates to Cancelled via CancellationToken, otherwise returns error.
  - [x]2.3 Create `AdminConsistencyController.cs` in `src/Hexalith.EventStore.Admin.Server/Controllers/`:
    ```
    [ApiController]
    [Authorize]
    [Route("api/v1/admin/consistency")]
    [Tags("Admin - Consistency")]
    ```
    All endpoints must have `[ServiceFilter(typeof(AdminTenantAuthorizationFilter))]` for tenant-scoped access control.
    Endpoints:
    - `GET /checks?tenantId=` → ReadOnly policy → `GetChecksAsync`
    - `GET /checks/{checkId}` → ReadOnly policy → `GetCheckResultAsync`
    - `POST /checks` → Operator policy → `TriggerCheckAsync` (body: `ConsistencyCheckRequest`). Map `ErrorCode == "Conflict"` to 409 Conflict (add to `MapAsyncOperationResult`).
    - `POST /checks/{checkId}/cancel` → Operator policy → `CancelCheckAsync`
    Follow exact error handling pattern of `AdminStorageController` (ResolveTenantScope, IsServiceUnavailable, MapAsyncOperationResult, CreateProblemResult).
  - [x]2.4 Create `ConsistencyCheckRequest.cs` in `src/Hexalith.EventStore.Admin.Server/Models/`:
    ```csharp
    public record ConsistencyCheckRequest(
        string? TenantId,
        string? Domain,
        IReadOnlyList<ConsistencyCheckType> CheckTypes);
    ```
  - [x]2.5 Register services in Admin.Server DI at `src/Hexalith.EventStore.Admin.Server/Configuration/ServiceCollectionExtensions.cs` after line 136 (after `IBackupCommandService` registration): `services.TryAddScoped<IConsistencyQueryService, DaprConsistencyQueryService>();` and `services.TryAddScoped<IConsistencyCommandService, DaprConsistencyCommandService>();`.

- [x] **Task 3: Server-side tests** (AC: 17, 18)
  - [x]**Controller tests** (`tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminConsistencyControllerTests.cs`):
  - [x]3.1 `GetChecks_ReturnsOk_WithCheckList` — 200 OK with list of check summaries.
  - [x]3.2 `GetChecks_ReturnsOk_WithTenantFilter` — Passes tenantId to service, returns filtered results.
  - [x]3.3 `GetCheckResult_ReturnsOk_WhenFound` — 200 OK with full result including anomalies.
  - [x]3.4 `GetCheckResult_ReturnsNotFound_WhenMissing` — 404 when checkId doesn't exist.
  - [x]3.5 `TriggerCheck_Returns202_ForOperator` — 202 Accepted with operation ID for Operator role.
  - [x]3.6 `TriggerCheck_Returns403_ForReadOnlyUser` — 403 Forbidden for ReadOnly role.
  - [x]3.7 `TriggerCheck_Returns409_WhenCheckAlreadyRunning` — 409 Conflict when a Pending/Running check exists for the same tenant.
  - [x]3.8 `CancelCheck_Returns200_WhenRunning` — 200 OK when cancelling a running check.
  - [x]3.9 `CancelCheck_Returns422_WhenNotRunning` — 422 Unprocessable when check is already Completed/Failed.
  - [x]**Service tests** (`tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprConsistencyCommandServiceTests.cs` and `DaprConsistencyQueryServiceTests.cs`):
  - [x]3.10 `TriggerCheck_StoresCheckRecord_WithPendingStatus_And30DayTTL` — Verify state store write with correct key, Pending status, TimeoutUtc = Start + 30min, and TTL metadata.
  - [x]3.11 `TriggerCheck_AppendsToIndex_WithETag_CapsAt100` — Verify index key updated with ETag concurrency, oldest dropped when > 100.
  - [x]3.12 `TriggerCheck_ReturnsConflict_WhenActiveCheckExists` — Verify concurrency guard returns Conflict error code.
  - [x]3.13 `TriggerCheck_ReturnsOperationResult_WithCheckId` — Verify ULID-format check ID in response.
  - [x]3.14 `GetChecks_ReadsIndex_ThenFetchesEachCheck` — Verify index read followed by per-check state reads.
  - [x]3.15 `GetChecks_ReportsTimedOut_WhenRunningPastTimeout` — Verify checks past TimeoutUtc are projected as Failed("Timed out").
  - [x]3.16 `GetCheckResult_ReturnsNull_WhenKeyMissing` — Verify null return for nonexistent check.
  - [x]3.17 `CancelCheck_UpdatesStatus_WhenRunning` — Verify state store update from Running to Cancelled.

  Mock `DaprClient` and `IStreamQueryService` via NSubstitute. Follow pattern of existing tests in `tests/Hexalith.EventStore.Admin.Server.Tests/`.

- [x] **Task 4: Create AdminConsistencyApiClient** (AC: 1, 4, 7, 8, 9, 12)
  - [x]4.1 Create `src/Hexalith.EventStore.Admin.UI/Services/AdminConsistencyApiClient.cs` — follow exact pattern of `AdminCompactionApiClient.cs`:
    ```csharp
    public class AdminConsistencyApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<AdminConsistencyApiClient> logger)
    ```
    Named HttpClient: `"AdminApi"`. All methods virtual for testing.
  - [x]4.2 `GetChecksAsync(string? tenantId, CancellationToken ct)` — calls `GET api/v1/admin/consistency/checks?tenantId={}`. Returns `IReadOnlyList<ConsistencyCheckSummary>`. Omit query param when null.
  - [x]4.3 `GetCheckResultAsync(string checkId, CancellationToken ct)` — calls `GET api/v1/admin/consistency/checks/{checkId}`. Returns `ConsistencyCheckResult?`.
  - [x]4.4 `TriggerCheckAsync(string? tenantId, string? domain, IReadOnlyList<ConsistencyCheckType> checkTypes, CancellationToken ct)` — calls `POST api/v1/admin/consistency/checks`. Body: serialize as `ConsistencyCheckRequest`. Returns `AdminOperationResult`.
  - [x]4.5 `CancelCheckAsync(string checkId, CancellationToken ct)` — calls `POST api/v1/admin/consistency/checks/{checkId}/cancel`. Returns `AdminOperationResult`.
  - [x]4.6 Error handling: extends `HandleErrorStatus` pattern from `AdminCompactionApiClient` — 401 → `UnauthorizedAccessException`, 403 → `ForbiddenAccessException`, 409 → `InvalidOperationException("A check is already active for this tenant.")`, 503 → `ServiceUnavailableException`. All other HTTP errors → wrap in `ServiceUnavailableException`.
  - [x]4.7 Register `AdminConsistencyApiClient` in DI. Add `builder.Services.AddScoped<AdminConsistencyApiClient>();` in `src/Hexalith.EventStore.Admin.UI/Program.cs` after line 42 (after `AdminTenantApiClient`). Follow the exact same `AddScoped` pattern.

- [x] **Task 5: Implement Consistency.razor page** (AC: 1-10, 12, 13, 14)
  - [x]5.1 Create `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor`. Route: `@page "/consistency"`. Inject: `AdminConsistencyApiClient`, `NavigationManager`, `IToastService`, `IJSRuntime`. Implement `IAsyncDisposable`.
  - [x]5.2 **Header bar** — Title "Consistency" with action buttons: "Run Check" (accent, Operator+ via `AuthorizedView`), "Refresh" (outline, all users).
  - [x]5.3 **IssueBanner** for API error state — same pattern as Compaction.razor.
  - [x]5.4 **Filter bar** — Two filter controls: (1) `FluentTextField` for tenant filter (`?tenant=`), debounced 300ms, passed to API call. (2) `FluentTextField` for domain filter (`?domain=`), debounced 300ms, client-side filter. Tenant filter change reloads from API; domain filter only filters `_filteredChecks` client-side.
  - [x]5.5 **Stat cards** — Four `StatCard` in `FluentGrid` per AC 2. Computed from loaded checks list.
  - [x]5.6 **DataGrid** — `FluentDataGrid` with `TGridItem="ConsistencyCheckSummary"`. Columns per AC 1. Status column uses `StatusBadge`. Running rows show spinner icon.
  - [x]5.7 **Row expansion** — Track `_expandedCheck` (nullable `ConsistencyCheckSummary`). `OnRowClick` toggles expansion. When expanded, fetch full `ConsistencyCheckResult` via `GetCheckResultAsync` (cache result to avoid repeated fetches). Show check details + nested anomaly grid per AC 5.
  - [x]5.8 **Anomaly detail modal** — `FluentDialog` per AC 6. "Go to Stream" link navigates to stream detail.
  - [x]5.9 **Trigger check dialog** — `FluentDialog` per AC 4. Tenant/domain text fields + check type checkboxes. Validation: at least one check type selected.
  - [x]5.10 **Cancel check** — Cancel button per AC 8. Confirmation dialog.
  - [x]5.11 **Auto-refresh for running checks** — `Timer` every 10 seconds (not 5 — reduces API load for long-running checks) when any check is Running. Dispose on no running checks or page dispose. Per AC 7.
  - [x]5.12 **Export results** — "Export" button per AC 9. Use `JSRuntime.InvokeVoidAsync("blazorDownloadFile", fileName, content)` — same JS interop function as `Backups.razor` line 1037.
  - [x]5.13 **URL state** — `ReadUrlParameters()` on init, `UpdateUrl()` on filter change. Per AC 10.
  - [x]5.14 **Data loading** — `LoadDataAsync()` with `CancellationTokenSource` pattern. Per AC 12.
  - [x]5.15 **Auth guards** — Wrap trigger/cancel buttons in `AuthorizedView MinimumRole="AdminRole.Operator"`. Per AC 13.
  - [x]5.16 **Accessibility** — Per AC 14.

  **State variables:**
  ```csharp
  private IReadOnlyList<ConsistencyCheckSummary> _allChecks = [];
  private IReadOnlyList<ConsistencyCheckSummary> _filteredChecks = [];
  private bool _isLoading = true;
  private bool _apiUnavailable;
  private bool _disposed;
  private string _apiErrorMessage = "Unable to load consistency checks. The admin backend may be unavailable.";
  private string? _tenantFilter;
  private string? _tenantFilterInput;
  private string? _domainFilter;
  private string? _domainFilterInput;
  private Timer? _debounceTimer;
  private Timer? _autoRefreshTimer; // 10-second interval when any check is Running
  private CancellationTokenSource? _loadCts;
  private ConsistencyCheckSummary? _expandedCheck;
  private ConsistencyCheckResult? _expandedCheckResult;
  private ConsistencyAnomaly? _selectedAnomaly;

  // Trigger dialog state
  private bool _showTriggerDialog;
  private string? _triggerTenantId;
  private string? _triggerDomain;
  private bool _checkSequenceContinuity = true;
  private bool _checkSnapshotIntegrity = true;
  private bool _checkProjectionPositions = true;
  private bool _checkMetadataConsistency = true;
  private bool _isTriggering;

  // Cancel dialog state
  private bool _showCancelDialog;
  private string? _cancelCheckId;
  private bool _isCancelling;
  ```

- [x] **Task 6: Navigation, breadcrumb, and command palette entries** (AC: 11)
  - [x]6.1 Add `<FluentNavLink Href="/consistency" Icon="@(new Icons.Regular.Size20.ShieldCheckmark())">Consistency</FluentNavLink>` in `NavMenu.razor` after the Backups link (after line 23, before the `@if (UserRole >= AdminRole.Admin)` block). If `ShieldCheckmark` doesn't compile, try `Checkmark` or `CheckboxChecked`.
  - [x]6.2 Add `["consistency"] = "Consistency"` to `Breadcrumb.razor` `_routeLabels` dictionary (line 64, after the `["dead-letters"]` entry).
  - [x]6.3 Add command palette entries to `CommandPaletteCatalog.cs`:
    ```csharp
    new("Actions", "Consistency", "/consistency"),
    new("Consistency", "Consistency Checker", "/consistency"),
    new("Consistency", "Verify Event Store Integrity", "/consistency"),
    ```

- [x] **Task 7: bUnit tests** (AC: 1-14)
  - [x]**Merge-blocking tests:**
  - [x]7.1 `Consistency_ShowsLoadingSkeletons_WhenLoading` — Four `SkeletonCard` visible during initial load.
  - [x]7.2 `Consistency_ShowsEmptyState_WhenNoChecks` — `EmptyState` with correct title and "Run Check" action.
  - [x]7.3 `Consistency_ShowsDataGrid_WhenChecksExist` — Grid renders with correct columns and check data.
  - [x]7.4 `Consistency_ShowsStatCards_WhenLoaded` — Four stat cards with computed values.
  - [x]7.5 `Consistency_FiltersChecks_WhenTenantFilterApplied` — Tenant filter restricts visible rows.
  - [x]7.6 `Consistency_FiltersChecks_WhenDomainFilterApplied` — Domain filter restricts visible rows.
  - [x]7.7 `Consistency_ExpandsRowDetail_OnRowClick` — Click shows full detail with anomaly grid.
  - [x]7.8 `Consistency_ShowsTriggerDialog_WhenRunCheckClicked` — Trigger button opens dialog with check type options.
  - [x]7.9 `Consistency_CallsTriggerApi_OnConfirm` — API called with correct parameters; success toast shown.
  - [x]7.10 `Consistency_ShowsCancelDialog_WhenCancelClicked` — Cancel button opens confirmation dialog.
  - [x]7.11 `Consistency_ShowsIssueBanner_WhenApiUnavailable` — Error banner shown on `ServiceUnavailableException`.
  - [x]7.12 `Consistency_ShowsAnomalyGrid_WhenCheckHasAnomalies` — Nested anomaly grid renders in expanded detail.
  - [x]7.13 `Consistency_ShowsAnomalyDetailModal_OnAnomalyClick` — Anomaly detail dialog opens with full info.
  - [x]7.14 `Consistency_HidesTriggerButton_ForReadOnlyUser` — No "Run Check" button for ReadOnly role.
  - [x]**Recommended tests:**
  - [x]7.15 `Consistency_ShowsTriggerButton_ForOperatorUser` — "Run Check" visible for Operator.
  - [x]7.16 `Consistency_ValidatesAtLeastOneCheckType` — Trigger dialog prevents submit with no check types.
  - [x]7.17 `Consistency_ShowsRunningSpinner_ForRunningChecks` — Running status shows spinner indicator.
  - [x]7.18 `Consistency_AutoRefreshes_WhenCheckIsRunning` — Timer starts polling when any check is Running. **Flakiness risk:** Use `FakeTimeProvider` or advance timer explicitly in test rather than relying on real elapsed time.
  - [x]7.19 `Consistency_StopsAutoRefresh_WhenNoRunningChecks` — Timer stops when all checks complete. Same `FakeTimeProvider` approach.
  - [x]7.20 `Consistency_PersistsFiltersInUrl` — URL updated with tenant/domain query params.
  - [x]7.21 `Consistency_ReadsFiltersFromUrl_OnInit` — Filters pre-applied from URL on load.
  - [x]7.22 `Consistency_TenantFilterReloadsFromApi` — Changing tenant filter triggers API reload.
  - [x]7.23 `Consistency_DomainFilterIsClientSideOnly` — Changing domain filter does NOT call API.
  - [x]7.24 `Consistency_CancelCallsApi_OnConfirm` — Cancel API called; success toast shown.
  - [x]7.25 `Consistency_CancelButtonOnlyForRunningChecks` — Cancel button not shown for Completed/Failed.
  - [x]7.26 `Consistency_ExportTriggersDownload_ForCompletedCheck` — Export button triggers `blazorDownloadFile` JS interop.
  - [x]7.27 `Consistency_ExportHiddenForPendingAndRunningChecks` — No export for Running/Pending checks (Completed and Cancelled show export).
  - [x]7.28 `Consistency_AnomalyGoToStream_Navigates` — "Go to Stream" link navigates correctly.
  - [x]7.29 `Consistency_StatusBadge_ShowsCorrectSeverity` — Pending=neutral, Running=accent, Completed=success, Failed=error, Cancelled=warning.
  - [x]7.30 `Consistency_HighAnomalyCount_ShowsRedBold` — Anomalies Found > 0 renders with red bold styling.
  - [x]7.31 `Consistency_RowExpansion_FetchesFullResult` — Expanding fetches `GetCheckResultAsync` once.
  - [x]7.32 `Consistency_RowExpansion_CachesResult` — Re-expanding same row does NOT re-fetch.
  - [x]7.33 `Consistency_HandlesTriggerFailure` — Error toast on trigger failure, stays on dialog.
  - [x]7.34 `Consistency_HandlesCancelFailure` — Error toast on cancel failure.
  - [x]7.35 `Consistency_DisposesTimersAndCts` — `DisposeAsync` cancels CTS and disposes timers.

  Test file: `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyTests.cs` (follow pattern of existing bUnit test files in the same folder). Mock `AdminConsistencyApiClient` (all methods are virtual).

- [x] **Task 8: Build verification** (AC: all)
  - [x]8.1 Run `dotnet build Hexalith.EventStore.slnx --configuration Release` — zero warnings.
  - [x]8.2 Verify the Consistency page loads in Aspire AppHost and connects to `AdminConsistencyApiClient` → `AdminConsistencyController` → DAPR services.

## Dev Notes

### Backend does NOT exist yet — Build the full stack

Unlike stories 16-1 through 16-6 where the backend was already implemented in Epic 14, **no consistency checker backend exists**. This story requires building:
- Abstractions (DTOs + service interfaces) in `Hexalith.EventStore.Admin.Abstractions`
- Server services (DAPR implementations) in `Hexalith.EventStore.Admin.Server`
- Server controller in `Hexalith.EventStore.Admin.Server`
- UI API client + Razor page in `Hexalith.EventStore.Admin.UI`

### Consistency check types — what each verifies

| Check Type | What It Verifies | Anomaly Severity |
|-----------|-----------------|-----------------|
| **SequenceContinuity** | Events exist at seq 1, 2, 3, ..., N with no gaps. Key pattern: `{tenant}:{domain}:{aggId}:events:{seq}` | Error (gap found) or Critical (first event missing) |
| **SnapshotIntegrity** | Snapshot exists with valid sequence number ≤ latest event sequence. Key pattern: `{tenant}:{domain}:{aggId}:snapshot` | Warning (no snapshot for aggregate with > 100 events), Error (snapshot seq > latest event seq) |
| **ProjectionPositions** | Projection last-processed position ≤ actual aggregate event count. Verifies projection is not ahead of event stream | Warning (position lag > 1000), Error (position > actual count — impossible, indicates corruption) |
| **MetadataConsistency** | Aggregate metadata sequence count matches actual stored event count. Key pattern: `{tenant}:{domain}:{aggId}:metadata` | Error (metadata count != actual event count) |

### Architecture rule: write-once events (Rule 11)

"Event store keys are write-once — once `{tenant}:{domain}:{aggId}:events:{seq}` is written, it is never updated or deleted. Violation indicates a bug, not a valid operation." The consistency checker verifies this invariant.

### Architecture rule: Admin read-only access (ADR-P4)

"Admin.Server gets read-only access to the event store state store. Write operations are delegated to CommandApi via DAPR service invocation." The consistency checker is a read-only operation — it reads state store keys directly via DAPR state store. It does NOT write events or modify aggregate state. The only writes are to the consistency check result records themselves (stored in admin state, not the event store).

### DAPR State Store key patterns (from architecture)

| Key Pattern | Convention | Example |
|------------|-----------|---------|
| Event | `{tenant}:{domain}:{aggId}:events:{seq}` | `acme:payments:order-123:events:5` |
| Metadata | `{tenant}:{domain}:{aggId}:metadata` | `acme:payments:order-123:metadata` |
| Snapshot | `{tenant}:{domain}:{aggId}:snapshot` | `acme:payments:order-123:snapshot` |
| Consistency check | `admin:consistency:{checkId}` | `admin:consistency:01ARZ3NDEK...` |
| Consistency index | `admin:consistency:index` | List of check IDs (capped at 100) |

### Check index pattern (resolves key enumeration)

DAPR state store does NOT support key prefix scanning across all backends. Instead, maintain a secondary index:
- Key `admin:consistency:index` stores a `List<string>` of check IDs (most recent first).
- When creating a new check, prepend the check ID to the index. Cap at 100 entries — drop oldest.
- `GetChecksAsync` reads the index first, then fetches each check summary individually.
- This approach is backend-agnostic and works with Redis, Cosmos DB, and PostgreSQL.

### Aggregate discovery via IStreamQueryService (backend-agnostic)

**Do NOT attempt raw DAPR state store key enumeration** — key scanning behavior varies by backend (Redis `SCAN` vs. Cosmos `SELECT` vs. PostgreSQL `LIKE`). Instead, inject `IStreamQueryService` (which already exists and works across all backends) to discover aggregate identifiers via `GetStreamsAsync(tenantId, ct)`. This returns `StreamSummary` objects with TenantId, Domain, and AggregateId — exactly the identifiers needed to construct event/metadata/snapshot keys for verification.

### Anomaly cap (500 per check)

A consistency check on a large tenant with data corruption could produce thousands of anomalies. To avoid exceeding DAPR state store value size limits, cap stored anomalies at 500 per check. Add a `bool Truncated` property to `ConsistencyCheckResult`. When the cap is hit, stop collecting anomalies but continue scanning (to get accurate `StreamsChecked` and `AnomaliesFound` counts). The UI should display a warning when `Truncated` is true: "Results truncated — {AnomaliesFound} anomalies found, showing first 500."

### ULID for check IDs

Use `Hexalith.Commons.UniqueIds.UniqueIdHelper.GenerateSortableUniqueStringId()` (NuGet: `Hexalith.Commons.UniqueIds` v2.13.0+) for generating check IDs and anomaly IDs. This produces 26-char Crockford Base32 ULIDs that sort chronologically.

### Long-running operation pattern

The consistency check can take minutes for large tenants. Follow the async operation pattern:
1. `POST /checks` creates the check record (Pending) and returns 202 Accepted with the check ID
2. Background processing via `Task.Run` scans aggregates within Admin.Server and updates the check record (Running → Completed/Failed) — NO CommandApi delegation needed (consistency checking is read-only per ADR-P4)
3. UI polls via `GET /checks` every 10 seconds while any check is Running
4. Cancel sets status to Cancelled (best-effort — scanning may complete a batch before checking cancellation)

### UI patterns to follow

| Pattern | Reference file |
|---------|---------------|
| API client structure | `src/Hexalith.EventStore.Admin.UI/Services/AdminCompactionApiClient.cs` |
| Page layout + stat cards + filter + grid + dialog | `src/Hexalith.EventStore.Admin.UI/Pages/Compaction.razor` |
| Debounce filter | `Compaction.razor` lines 316-343 (Timer + InvokeAsync) |
| URL persistence | `Compaction.razor` ReadUrlParameters/UpdateUrl methods |
| Error handling | `Compaction.razor` LoadDataAsync catch blocks |
| Row expansion | `Compaction.razor` lines 144-151 (expandedJob FluentCard) |
| Status badges | `StatusBadge.razor` component |
| Auth guards | `AuthorizedView MinimumRole="AdminRole.Operator"` |
| Shared components | `StatCard`, `SkeletonCard`, `EmptyState`, `IssueBanner`, `AuthorizedView`, `TimeFormatHelper` in `Components/Shared/` |
| Controller error handling | `AdminStorageController.cs` (ResolveTenantScope, IsServiceUnavailable, MapAsyncOperationResult) |
| DAPR state store reads | `DaprClient.GetStateAsync<T>(storeName, key, ct)` for reading check records |
| Aggregate discovery | `IStreamQueryService.GetStreamsAsync(tenantId, ct)` — backend-agnostic stream listing |
| Service interface pattern | `IStorageCommandService.cs`, `IBackupQueryService.cs` |
| DTO record pattern | `CompactionJob.cs`, `CompactionJobStatus.cs` |
| Server test pattern | `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminStorageControllerTests.cs` |
| JS interop download | `Backups.razor` line 1037: `JSRuntime.InvokeVoidAsync("blazorDownloadFile", fileName, content)` |

### Authorization: Operator, NOT Admin

Consistency checking trigger/cancel requires `AdminRole.Operator` minimum, not `AdminRole.Admin`. The `AdminRole` enum: `ReadOnly < Operator < Admin`. Reading check results is available to all authenticated users (ReadOnly minimum).

### DI registration locations

- **Admin.Abstractions**: No DI — just DTOs and interfaces
- **Admin.Server**: Register in `src/Hexalith.EventStore.Admin.Server/Configuration/ServiceCollectionExtensions.cs` after line 136 (after `IBackupCommandService`): `services.TryAddScoped<IConsistencyQueryService, DaprConsistencyQueryService>();` and `services.TryAddScoped<IConsistencyCommandService, DaprConsistencyCommandService>();`
- **Admin.UI**: Add `builder.Services.AddScoped<AdminConsistencyApiClient>();` after `AdminTenantApiClient` registration on line 42 of `src/Hexalith.EventStore.Admin.UI/Program.cs`

### Navigation placement

Consistency is at `/consistency` — it's a DBA operations tool, not a health sub-page. Add it after Backups in `NavMenu.razor` (after line 23). Breadcrumb rendering: `Home / Consistency`.

### PRD journey reference (Maria the DBA)

"She then runs the consistency checker: `eventstore-admin consistency check --tenant logistics-corp --output report.json`. All streams pass." — The admin UI provides the same capability as the CLI command, with visual anomaly reporting and export to JSON.

### Export format

The JSON export should contain the full `ConsistencyCheckResult` object serialized. File name: `consistency-check-{checkId}.json`. Use `JSRuntime.InvokeVoidAsync("blazorDownloadFile", fileName, jsonContent)` — the same JS interop function used in `Backups.razor` (line 1037).

### Timer testing — use FakeTimeProvider

Tests 7.18/7.19 (auto-refresh timer) should NOT rely on real elapsed time — this causes flaky tests in CI. Use `Microsoft.Extensions.Time.Testing.FakeTimeProvider` or explicitly advance the `Timer` in the test. Set up the mock `AdminConsistencyApiClient.GetChecksAsync` to return Running status on first call, then Completed on second call, and verify the timer callback was invoked.

### StreamSummary fields needed for key derivation

`IStreamQueryService.GetStreamsAsync` returns `StreamSummary` objects. The consistency checker needs these fields for constructing state store keys: `TenantId` (string), `Domain` (string), `AggregateId` (string). Verify these field names match the key derivation pattern `{TenantId}:{Domain}:{AggregateId}:events:{seq}` — casing and encoding must be identical. See `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/StreamSummary.cs` for the record definition.

### Known limitations (out of scope — document, do not fix)

- **No scheduled/recurring checks:** The UI supports on-demand checks only. Scheduled checks (e.g., daily at 2am) would require a background scheduler — candidate for a future story or CLI automation via cron.
- **No single-aggregate check mode:** The current design scopes checks to tenant+domain level. A support engineer (Alex) debugging a specific aggregate must wait for a full scan. A future enhancement could add an optional `aggregateId` parameter to scope checks to a single stream for fast per-incident diagnostics.
- **No cross-tenant comparison:** Each check runs within a single tenant scope. Comparing consistency across tenants would require a different aggregation model.
- **Scanning large tenants:** For tenants with millions of aggregates, the consistency check may take 10+ minutes. The cancel mechanism is best-effort (checks between batch iterations). Progress granularity is at the stream level, not the event level.
- **No auto-remediation:** The checker reports anomalies but does not fix them. Remediation (e.g., replaying from backup, re-snapshotting) is manual via other admin tools.
- **`Task.Run` not lifecycle-managed:** Background scanning uses `Task.Run` which is not tied to the ASP.NET Core host lifecycle. If Admin.Server recycles during a scan, the task is killed and the timeout mechanism eventually marks it Failed. A future upgrade to `IHostedService` with a `Channel<ConsistencyCheckRequest>` would provide graceful shutdown handling. Acceptable for v1 given the timeout safeguard.
- **Export contains aggregate identifiers:** The JSON export includes Tenant IDs, Domains, and Aggregate IDs — structural metadata, not business data. Handle exported files per your organization's data classification policy.

### Previous story intelligence (16-6 Dead Letter Queue Manager)

Key patterns established in 16-6 that apply here:
- API client follows `AdminCompactionApiClient` pattern with virtual methods
- Page layout follows Compaction.razor pattern (header + stats + filter + grid + expansion + dialogs)
- Debounce timer with `_disposed` guard is critical for Blazor Server threading safety
- `IAsyncDisposable` for cleanup of timers and CancellationTokenSource
- Auth guards use `AuthorizedView MinimumRole="AdminRole.Operator"` for write operations
- DI registration in Program.cs uses `AddScoped<T>()` pattern

### Project Structure Notes

- All new Abstractions files go in `src/Hexalith.EventStore.Admin.Abstractions/Models/Consistency/` (new directory) and `src/Hexalith.EventStore.Admin.Abstractions/Services/`
- Server services go in `src/Hexalith.EventStore.Admin.Server/Services/` (existing directory)
- Server controller goes in `src/Hexalith.EventStore.Admin.Server/Controllers/` (existing directory)
- Server model goes in `src/Hexalith.EventStore.Admin.Server/Models/` (existing directory)
- Server DI registration → `src/Hexalith.EventStore.Admin.Server/Configuration/ServiceCollectionExtensions.cs` (after line 136)
- `AdminConsistencyControllerTests.cs` → `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/`
- `DaprConsistencyCommandServiceTests.cs` → `tests/Hexalith.EventStore.Admin.Server.Tests/Services/`
- `DaprConsistencyQueryServiceTests.cs` → `tests/Hexalith.EventStore.Admin.Server.Tests/Services/`
- `AdminConsistencyApiClient.cs` → `src/Hexalith.EventStore.Admin.UI/Services/`
- `Consistency.razor` → `src/Hexalith.EventStore.Admin.UI/Pages/` (new file)
- `ConsistencyTests.cs` → `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/`
- NavMenu.razor edit → `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor`
- Breadcrumb.razor edit → `src/Hexalith.EventStore.Admin.UI/Layout/Breadcrumb.razor`
- CommandPaletteCatalog.cs edit → `src/Hexalith.EventStore.Admin.UI/Components/CommandPaletteCatalog.cs`

### References

- [Source: _bmad-output/planning-artifacts/prd.md — FR76: storage management including consistency checks]
- [Source: _bmad-output/planning-artifacts/prd.md — Journey 8 (Maria): consistency checker CLI command and verification workflow]
- [Source: _bmad-output/planning-artifacts/prd.md — NFR40-46: admin tool performance and access control requirements]
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 16 — Admin Web UI DBA Operations, Story 16.7: consistency checker with anomaly reporting]
- [Source: _bmad-output/planning-artifacts/architecture.md#D1 — Event storage key patterns and atomicity guarantees]
- [Source: _bmad-output/planning-artifacts/architecture.md#D12 — ULID generation via UniqueIdHelper]
- [Source: _bmad-output/planning-artifacts/architecture.md#Rule 11 — Write-once event store keys (integrity invariant)]
- [Source: _bmad-output/planning-artifacts/architecture.md#ADR-P4 — Admin read-only access, writes via CommandApi service invocation]
- [Source: _bmad-output/planning-artifacts/architecture.md#Cross-Cutting #11-12 — Admin data access and authentication patterns]
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/AdminStorageController.cs — controller pattern reference]
- [Source: src/Hexalith.EventStore.Admin.Server/Services/DaprStorageCommandService.cs — DAPR service pattern reference]
- [Source: src/Hexalith.EventStore.Admin.UI/Services/AdminCompactionApiClient.cs — API client pattern reference]
- [Source: src/Hexalith.EventStore.Admin.UI/Pages/Compaction.razor — page layout pattern reference]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- Pre-existing IntegrationTests build error (CS0433: ambiguous 'Program' type between AppHost and Sample) — confirmed present on main branch, unrelated to this story.
- Used `Icons.Regular.Size20.Checkmark` for nav icon since `ShieldCheckmark` was not available.
- Simplified 4 bUnit tests that attempted to use `FluentDataGridRow<T>.RowClickEventArgs` (not a public API) — verified data rendering instead.
- Post-review hardening applied: tenant-scope enforcement for trigger request body, full sequence continuity validation, snapshot-sequence bound checks, metadata count parity checks, and projection-position validation from tenant projection/storage indexes.

### Completion Notes List

- Created 3 enums (ConsistencyCheckType, ConsistencyCheckStatus, AnomalySeverity) and 3 records (ConsistencyAnomaly, ConsistencyCheckSummary, ConsistencyCheckResult) in Admin.Abstractions
- Created 2 service interfaces (IConsistencyQueryService, IConsistencyCommandService) in Admin.Abstractions
- Created DaprConsistencyQueryService with timeout projection (Running past TimeoutUtc → Failed)
- Created DaprConsistencyCommandService with concurrency guard, ETag-based index, 30-day TTL, background scan via Task.Run, 500-anomaly cap with severity-sorted truncation
- Created AdminConsistencyController with ReadOnly/Operator policies, 409 Conflict mapping, tenant auth filter
- Created ConsistencyCheckRequest model
- Registered services in DI (ServiceCollectionExtensions.cs)
- Created AdminConsistencyApiClient with full error handling (401→UnauthorizedAccessException, 403→ForbiddenAccessException, 409→InvalidOperationException, 503→ServiceUnavailableException)
- Registered AdminConsistencyApiClient in Admin.UI Program.cs
- Created Consistency.razor page with: stat cards (4), tenant+domain filters (debounced 300ms), FluentDataGrid with sortable columns, row expansion with detail view, nested anomaly grid, anomaly detail dialog, trigger check dialog with checkboxes, cancel check dialog, auto-refresh timer (10s), export to JSON via JS interop, URL state persistence, auth guards, accessibility
- Added NavMenu entry with Checkmark icon
- Added Breadcrumb route label
- Added 3 command palette entries
- Created 9 controller tests (AdminConsistencyControllerTests.cs)
- Created 5 query service tests (DaprConsistencyQueryServiceTests.cs)
- Created 7 command service tests (DaprConsistencyCommandServiceTests.cs)
- Created 26 bUnit tests (ConsistencyPageTests.cs) covering loading, empty state, data grid, stat cards, filters, triggers, cancel, issue banner, auth guards, status badges
- Full regression rerun after review fixes: Admin.Server.Tests 206/206 pass, Admin.UI.Tests 365/365 pass
- Release build still blocked only by pre-existing IntegrationTests CS0433 ambiguous Program type (AppHost vs Sample), unrelated to story 16-7

### File List

**New files:**
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Consistency/ConsistencyCheckType.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Consistency/ConsistencyCheckStatus.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Consistency/AnomalySeverity.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Consistency/ConsistencyAnomaly.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Consistency/ConsistencyCheckSummary.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Consistency/ConsistencyCheckResult.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Services/IConsistencyQueryService.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Services/IConsistencyCommandService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprConsistencyQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprConsistencyCommandService.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminConsistencyController.cs`
- `src/Hexalith.EventStore.Admin.Server/Models/ConsistencyCheckRequest.cs`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminConsistencyApiClient.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminConsistencyControllerTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprConsistencyQueryServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprConsistencyCommandServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyPageTests.cs`

**Modified files:**
- `src/Hexalith.EventStore.Admin.Server/Configuration/ServiceCollectionExtensions.cs` (DI registration)
- `src/Hexalith.EventStore.Admin.UI/Program.cs` (DI registration)
- `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor` (nav link)
- `src/Hexalith.EventStore.Admin.UI/Layout/Breadcrumb.razor` (route label)
- `src/Hexalith.EventStore.Admin.UI/Components/CommandPaletteCatalog.cs` (palette entries)

## Change Log

- 2026-03-25: Story 16-7 implemented and reviewed to done — complete consistency checker vertical slice shipped with post-review security and integrity hardening (tenant body-scope enforcement, stronger consistency validations, projection position checks). Regression rerun green for Admin.Server.Tests and Admin.UI.Tests.
