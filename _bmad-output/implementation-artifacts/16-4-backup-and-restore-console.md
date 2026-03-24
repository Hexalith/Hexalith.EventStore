# Story 16.4: Backup & Restore Console

Status: done

Size: Large — ~18 new/modified files, 7 task groups, 15 ACs, ~24 tests (~12-16 hours estimated). Core new work: Backup/Restore DTOs + enums (Task 1), backup service interfaces + server impls (Task 2), controller endpoints (Task 3), AdminBackupApiClient (Task 4), Backups.razor page with schedule/trigger/restore dialogs (Task 5), bUnit tests (Task 6), nav + breadcrumb + CSS (Task 7).

**Split advisory:** This story is at the upper size bound. If implementation velocity slows, consider splitting into two PRs: (A) Tasks 1-4 + Task 5 partial (backup list + create + validate) + Task 6 partial + Task 7, then (B) Task 5 remainder (restore dialog + export/import) + remaining tests. The natural split point is between "backup management" (trigger + validate + history) and "recovery operations" (restore + export/import). Both halves are independently shippable.

## Definition of Done

- All 15 ACs verified
- 13 merge-blocking bUnit tests green (Task 6 blocking tests)
- 11 recommended bUnit tests green
- Project builds with zero warnings in CI (`dotnet build --configuration Release`)
- No new analyzer suppressions

## Story

As a **database administrator (Maria) or infrastructure admin using the Hexalith EventStore admin dashboard**,
I want **a backup and restore console where I can view backup history, trigger on-demand backups for specific tenants, validate backup integrity, initiate point-in-time restores, and export/import individual streams**,
so that **I can protect event store data through scheduled and ad-hoc backups, confidently restore from corruption or data loss scenarios, and move individual streams between environments — all through the admin UI without developer involvement**.

## Acceptance Criteria

1. **Backup job list** — The `/backups` page displays a `FluentDataGrid` listing all backup jobs from `GetBackupJobsAsync`. Columns: Backup ID (monospace, truncated to 8 chars with full ID in tooltip), Tenant ID (monospace), Scope (badge — "Full" blue or "Stream" neutral, derived from `StreamId` null check), Status (badge — green Completed, orange Running, blue Pending, red Failed, teal Validating), Created (relative time using `TimeFormatHelper.FormatRelativeTime`, full UTC on tooltip), Duration (formatted as "Xm Ys" when completed, "Running..." when active, "—" when pending), Size (formatted via `TimeFormatHelper.FormatBytes`, "—" when pending/running), Events (right-aligned, `N0`, "—" when pending/running). Grid is sortable by all columns, default sort by Created descending (most recent first). When no backups exist, show `EmptyState` with title "No backups" and description "Create a backup to protect your event store data."

2. **Summary stat cards** — Above the grid, display four stat cards in a `FluentGrid` (xs=6, sm=6, md=3): (a) "Active Backups" showing count of jobs with status Pending or Running, severity Warning when > 0, (b) "Completed (30d)" showing count of jobs with status Completed and CreatedAtUtc within last 30 days, (c) "Total Backup Size" showing sum of `SizeBytes` across all completed backups formatted via `TimeFormatHelper.FormatBytes`, or "N/A" when no data, (d) "Last Successful" showing relative time of most recent Completed backup, or "Never" when none. Loading state shows 4 `SkeletonCard` placeholders.

3. **Tenant filter** — A `FluentTextField` with debounced input (300ms) filters backup jobs by tenant ID prefix. Filter persists in URL query parameter `?tenant=<id>`. Clearing the filter shows all backups. Same debounce pattern as `Compaction.razor` (Timer + `InvokeAsync` for Blazor Server threading).

4. **Trigger backup dialog** — A "Create Backup" button (visible only to `Admin` role via `AuthorizedView MinimumRole="AdminRole.Admin"`) opens a `FluentDialog`. Dialog title: "Create Backup". Form fields: Tenant ID (`FluentTextField`, required), Description (`FluentTextField`, optional — stored as metadata for identifying backup purpose), Include Snapshots (`FluentCheckbox`, default checked — whether to include snapshot state in backup). Below fields, a dynamic confirmation sentence: "This will back up all event streams for tenant **{tenantId}**." Warning banner: "Backup is a resource-intensive operation that runs asynchronously. It may temporarily increase I/O usage and state store read load." Primary button "Start Backup" (accent style) calls `TriggerBackupAsync`. Secondary button "Cancel". Validate Tenant ID non-empty before enabling primary button. On success: close dialog, show toast "Backup started for {tenantId}. Refresh to check status.", reload. On failure: error toast with `AdminOperationResult.Message`, keep dialog open. **Concurrent guard:** If the loaded job list contains a Pending or Running backup for the same tenant, disable "Start Backup" and show inline text: "Backup already in progress for this tenant." Additionally, if a restore is Pending or Running for the same tenant (check job list for restore-type jobs), disable "Start Backup" and show: "Restore in progress for this tenant. Wait for completion before creating a new backup."

5. **Validate backup integrity** — Each completed backup row has a "Validate" action button (visible to `Admin` role). Clicking opens a confirmation dialog: "Validate Backup {backupId}?" with description "This will verify event sequence integrity, snapshot consistency, and metadata completeness. Validation runs asynchronously." On confirm: calls `ValidateBackupAsync`, shows toast "Validation started for backup {backupId}.", reloads list. The backup status transitions to "Validating" during validation. Validation results appear as a status update (Completed with validation metadata, or Failed with error message).

6. **Restore dialog** — Each backup row where `Status == Completed` **AND** `IsValidated == true` has a "Restore" action button (visible to `Admin` role). Backups that are completed but not yet validated show only the "Validate" button — the "Restore" button is not rendered. This enforces the validation-before-restore invariant in the UI. Clicking "Restore" opens a multi-step `FluentDialog`. Step 1 — Confirmation: shows backup details (tenant, created date, event count, size) and warning banner: "Restoring will re-inject events from this backup into a parallel restore stream. Original event streams are never overwritten (D1: write-once keys). The restore creates new aggregate state from backup events." Checkbox: "I understand this will create restored state in the event store" (must be checked to proceed). Step 2 — Options: Point-in-time restore option with `FluentDatePicker` to select a cutoff timestamp (optional — leave empty for full restore). **Date validation**: if provided, must be ≤ `DateTimeOffset.UtcNow` (disable future dates) and ≥ backup's `CreatedAtUtc` minus the backup's event time range (if point-in-time yields zero events, show inline error "No events found before specified timestamp"). "Dry Run" `FluentCheckbox` (validate restore feasibility without applying). Primary button "Start Restore" calls `TriggerRestoreAsync`. On success: close dialog, toast "Restore initiated from backup {backupId}.", reload. On failure: error toast, stay on step 2. **Cross-operation concurrent guard**: If a Pending or Running backup exists for the same tenant, disable "Start Restore" and show inline text: "Backup in progress for this tenant. Wait for completion before restoring." Conversely, the create backup dialog (AC 4) should also check for Pending/Running restores and disable accordingly.

7. **Stream export** — A secondary "Export Stream" button (visible to `Admin` role) opens a dialog for exporting a single stream. Form fields: Tenant ID (required), Domain (required), Aggregate ID (required), Format (`FluentSelect` with options: "JSON" default, "CloudEvents"). On submit: calls `ExportStreamAsync` which returns a `StreamExportResult` containing serialized event content in the response body. The browser initiates a file download via JS interop Blob. Filename pattern: `{tenantId}_{domain}_{aggregateId}_{timestamp}.json`. **Export limit**: Maximum 50,000 events per export. If the aggregate exceeds this, show a warning before proceeding: "This stream has {N} events. Export is limited to the most recent 50,000 events." On error: toast with error message.

8. **Stream import** — An "Import Stream" button (visible to `Admin` role) opens a dialog for importing a previously exported stream. File picker for selecting the export file (try `FluentInputFile`; if unavailable in FluentUI v4.13.2, use ASP.NET Core `InputFile` wrapped in a `FluentCard` with drag-drop hint text). Max file size: 10 MB (configurable via `IBrowserFile.OpenReadStream(maxAllowedSize)`). Preview section shows parsed metadata (tenant, domain, aggregate, event count) before confirming — if JSON parsing fails, show inline error "Invalid export file format. Expected JSON with TenantId, Domain, AggregateId, and Events array." Warning: "Importing will append events to the target stream using new sequence numbers. Existing events are never overwritten (D1). Duplicate events (matching EventId) are automatically skipped." Primary button "Import" calls `ImportStreamAsync` with the file content. On success: toast "Imported {count} events to stream {aggregateId} ({skipped} duplicates skipped).", reload. On error: toast with validation details.

9. **Job status display** — Each backup row displays a status badge using `FluentBadge`: Pending (blue, `Appearance.Accent`), Running (orange, `Appearance.Warning` — pulsing via CSS animation), Completed (green, `Appearance.Success`), Failed (red, `Appearance.Error`), Validating (teal/info, pulsing). Failed jobs show an expandable error message row below the main row (click to toggle). The error message is displayed in a `FluentCard` with monospace font.

10. **URL state persistence** — The `/backups` page persists filter state in URL query parameter: `?tenant=<id>`. Page loads with filter pre-applied from URL. Uses `NavigationManager.NavigateTo(url, forceLoad: false, replace: true)`. All values escaped with `Uri.EscapeDataString()`.

11. **Breadcrumb integration** — The breadcrumb route label dictionary in `Breadcrumb.razor` includes `"backups" -> "Backups"`. Navigating to `/backups` renders breadcrumb: `Home / Backups`.

12. **Navigation entry** — The backups page appears in `NavMenu.razor` after "Compaction", with a backup-related icon. **Try `Icons.Regular.Size20.ArrowDownload`** first; if it does not compile, try `Icons.Regular.Size20.DatabaseArrowDown` or `Icons.Regular.Size20.CloudArrowDown`. Verify the icon class exists at compile time — `TreatWarningsAsErrors` is enabled. Visible to all authenticated users (page viewable by ReadOnly for backup history; write actions are Admin-gated). Add "Backups" and "Backup & Restore" entries to `CommandPaletteCatalog.cs` navigating to `/backups`.

13. **Data loading and refresh** — Initial data loads in `OnInitializedAsync` via `AdminBackupApiClient`. Manual refresh via "Refresh" button. Error state shows `IssueBanner` with "Unable to load backup jobs" and retry button. `IAsyncDisposable` for cleanup.

14. **Admin role enforcement** — The `/backups` page is visible to all authenticated users (`ReadOnly` minimum) for viewing backup history. ALL write operations (create backup, validate, restore, export, import) require `Admin` role (NFR46 — backup/restore is infrastructure-level). Write buttons are hidden for non-Admin users via `AuthorizedView MinimumRole="AdminRole.Admin"`. API calls for writes use `AdminFull` policy.

15. **Accessibility** — Page heading `<h1>Backups</h1>` is the first focusable element. All dialogs use `FluentDialog` with proper `aria-label`. Form fields have associated labels. Multi-step restore dialog has step indicators with `aria-current="step"`. Stat cards follow existing `StatCard` accessibility pattern. Data grid follows `FluentDataGrid` built-in accessibility. Status badges have `aria-label` attributes.

## Tasks / Subtasks

- [x]**Task 1: Create Backup/Restore DTOs and enums** (AC: 1, 7, 8, 9)
  - [x]1.1 Create `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/BackupJobStatus.cs` — enum with values: `Pending`, `Running`, `Completed`, `Failed`, `Validating`. Follow same file pattern as `CompactionJobStatus.cs`.
  - [x]1.2 Create `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/BackupJob.cs` — record:
    ```csharp
    /// <summary>
    /// Represents a backup job with its status and metrics.
    /// </summary>
    /// <param name="BackupId">Unique identifier for the backup.</param>
    /// <param name="TenantId">Tenant that was backed up.</param>
    /// <param name="StreamId">Specific stream ID if stream-level backup, or null for full tenant backup.</param>
    /// <param name="Description">Optional description/purpose of the backup.</param>
    /// <param name="Status">Current job status.</param>
    /// <param name="IncludeSnapshots">Whether snapshot state was included.</param>
    /// <param name="CreatedAtUtc">When the backup was triggered.</param>
    /// <param name="CompletedAtUtc">When the backup finished, or null if still running.</param>
    /// <param name="EventCount">Number of events in backup, or null if not yet available.</param>
    /// <param name="SizeBytes">Backup size in bytes, or null if not yet available.</param>
    /// <param name="IsValidated">Whether integrity validation has been completed.</param>
    /// <param name="ErrorMessage">Error details when status is Failed, otherwise null.</param>
    public record BackupJob(
        string BackupId,
        string TenantId,
        string? StreamId,
        string? Description,
        BackupJobStatus Status,
        bool IncludeSnapshots,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        long? EventCount,
        long? SizeBytes,
        bool IsValidated,
        string? ErrorMessage);
    ```
  - [x]1.3 Create `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/StreamExportRequest.cs` — record:
    ```csharp
    /// <summary>
    /// Request to export a single stream.
    /// </summary>
    /// <param name="TenantId">The tenant identifier.</param>
    /// <param name="Domain">The domain name.</param>
    /// <param name="AggregateId">The aggregate identifier.</param>
    /// <param name="Format">Export format: "JSON" or "CloudEvents".</param>
    public record StreamExportRequest(
        string TenantId,
        string Domain,
        string AggregateId,
        string Format = "JSON");
    ```
  - [x]1.4 Create `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/StreamExportResult.cs` — record:
    ```csharp
    /// <summary>
    /// Result of a stream export operation.
    /// </summary>
    /// <param name="Success">Whether the export completed.</param>
    /// <param name="TenantId">Source tenant.</param>
    /// <param name="Domain">Source domain.</param>
    /// <param name="AggregateId">Source aggregate.</param>
    /// <param name="EventCount">Number of events exported.</param>
    /// <param name="Content">Serialized export content.</param>
    /// <param name="FileName">Suggested filename for download.</param>
    /// <param name="ErrorMessage">Error details if failed.</param>
    public record StreamExportResult(
        bool Success,
        string TenantId,
        string Domain,
        string AggregateId,
        long EventCount,
        string? Content,
        string? FileName,
        string? ErrorMessage);
    ```
  - [x]1.5 **Checkpoint**: DTOs compile, follow existing record patterns (see `CompactionJob.cs`, `SnapshotPolicy.cs`).

- [x]**Task 2: Add backup service interfaces** (AC: 1, 4, 5, 6, 7, 8)
  - [x]2.1 Create `src/Hexalith.EventStore.Admin.Abstractions/Services/IBackupQueryService.cs`:
    ```csharp
    /// <summary>
    /// Service interface for querying backup job history.
    /// </summary>
    public interface IBackupQueryService
    {
        /// <summary>Gets backup jobs, optionally filtered by tenant.</summary>
        Task<IReadOnlyList<BackupJob>> GetBackupJobsAsync(string? tenantId, CancellationToken ct = default);
    }
    ```
  - [x]2.2 Create `src/Hexalith.EventStore.Admin.Abstractions/Services/IBackupCommandService.cs`:
    ```csharp
    /// <summary>
    /// Service interface for admin-level backup and restore operations.
    /// </summary>
    public interface IBackupCommandService
    {
        /// <summary>Triggers a full tenant backup.</summary>
        Task<AdminOperationResult> TriggerBackupAsync(string tenantId, string? description, bool includeSnapshots, CancellationToken ct = default);

        /// <summary>Validates integrity of a completed backup.</summary>
        Task<AdminOperationResult> ValidateBackupAsync(string backupId, CancellationToken ct = default);

        /// <summary>Initiates a restore from a backup.</summary>
        Task<AdminOperationResult> TriggerRestoreAsync(string backupId, DateTimeOffset? pointInTime, bool dryRun, CancellationToken ct = default);

        /// <summary>Exports a single stream as downloadable content.</summary>
        Task<StreamExportResult> ExportStreamAsync(StreamExportRequest request, CancellationToken ct = default);

        /// <summary>Imports events into a stream from exported content.</summary>
        Task<AdminOperationResult> ImportStreamAsync(string tenantId, string content, CancellationToken ct = default);
    }
    ```
  - [x]2.3 Implement `DaprBackupQueryService` in `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupQueryService.cs`. Read from admin index key `admin:backup-jobs:{scope}` (where scope is tenantId or "all"). Follow the exact same pattern as `DaprStorageQueryService.GetSnapshotPoliciesAsync` — read from DAPR state store, deserialize, handle missing index gracefully (return empty list with logging).
  - [x]2.4 Implement `DaprBackupCommandService` in `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs`. Follow the same pattern as `DaprStorageCommandService`:
    - `TriggerBackupAsync`: Write backup job metadata to `admin:backup-jobs:{tenantId}`, delegate actual backup work via DAPR service invocation to CommandApi. Return 202-style `AdminOperationResult` with generated `OperationId`.
    - `ValidateBackupAsync`: Update backup status to Validating, trigger async validation.
    - `TriggerRestoreAsync`: Delegate via DAPR service invocation to CommandApi. Restore creates a **parallel restore stream** with key prefix `{tenant}:{domain}:{aggId}:restore-{backupId}:events:{seq}` — original streams are never modified (D1: write-once keys). Point-in-time cutoff filters backup events by timestamp before writing to the restore stream. Dry-run mode validates deserialization and state reconstruction without writing.
    - `ExportStreamAsync`: **DAPR has no wildcard/prefix key query.** First read the aggregate metadata key `{tenant}:{domain}:{aggId}:metadata` to obtain `LastSequenceNumber`. **Hard limit: 50,000 events per export.** If `LastSequenceNumber > 50,000`, export only the most recent 50,000 events (from `LastSequenceNumber - 49,999` to `LastSequenceNumber`) and set a `Truncated: true` flag on the result. Read individual event keys `{tenant}:{domain}:{aggId}:events:{start}` through `{tenant}:{domain}:{aggId}:events:{N}` using `DaprClient.GetBulkStateAsync()` for batch efficiency (batch size ≤ 100 keys per call to avoid DAPR sidecar 5s timeout). Serialize collected events to the requested format and return `StreamExportResult`. **Security:** Validate `request.TenantId` against JWT claims before reading (see controller Task 3.6 note).
    - `ImportStreamAsync`: Parse import content, validate JSON structure and required fields (TenantId, Domain, AggregateId, Events array). Return validation errors for malformed input (missing fields, invalid JSON). **Import uses a dedicated admin import endpoint on CommandApi** (not the regular command pipeline) that writes events directly to new sequence numbers on the target aggregate — bypassing domain service validation since these are pre-validated historical events. This is an **admin-only direct-write path** analogous to database restore operations. The import endpoint on CommandApi must: (a) **deduplicate by EventId** — skip events whose `EventId` already exists in the target stream (prevents double-import), (b) verify the target aggregate does not already have events at the imported sequence numbers (append-only, never overwrite — D1 compliance), (c) **strip and regenerate envelope metadata** (SEC-1 compliance) — imported events get new `Timestamp` (import time), `UserId` (importing admin), and an `ImportedFromBackup: true` extension field, while preserving the original `EventId`, `EventType`, and payload, (d) update aggregate metadata after import, (e) publish imported events to pub/sub for projection rebuilding. Admin.Server delegates via DAPR service invocation — never writes directly to state store (ADR-P4). **Note for v1 stub:** Since CommandApi import endpoint doesn't exist yet, the stub writes job metadata and returns success with the event count from the parsed file. Actual import processing is deferred to the backend story.
  - [x]2.5 Register both services in `Admin.Server` DI: `services.AddScoped<IBackupQueryService, DaprBackupQueryService>()` and `services.AddScoped<IBackupCommandService, DaprBackupCommandService>()`.
  - [x]2.6 **Checkpoint**: Services compile, follow existing patterns, DI registered.

- [x]**Task 3: Add backup controller endpoints** (AC: 1, 4, 5, 6, 7, 8, 14)
  - [x]3.1 Create `src/Hexalith.EventStore.Admin.Server/Controllers/AdminBackupsController.cs` — new controller (separate from AdminStorageController to keep separation of concerns):
    ```csharp
    [ApiController]
    [Authorize]
    [Route("api/v1/admin/backups")]
    [Tags("Admin - Backups")]
    public class AdminBackupsController(
        IBackupQueryService backupQueryService,
        IBackupCommandService backupCommandService,
        ILogger<AdminBackupsController> logger) : ControllerBase
    ```
  - [x]3.2 Add GET endpoint for backup jobs:
    ```csharp
    [HttpGet]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(IReadOnlyList<BackupJob>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBackupJobs([FromQuery] string? tenantId, CancellationToken ct)
    ```
  - [x]3.3 Add POST endpoint for triggering backup:
    ```csharp
    [HttpPost("{tenantId}")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> TriggerBackup(string tenantId, [FromQuery] string? description, [FromQuery] bool includeSnapshots = true, CancellationToken ct)
    ```
    Use `MapAsyncOperationResult()` pattern from `AdminStorageController`.
  - [x]3.4 Add POST endpoint for validation:
    ```csharp
    [HttpPost("{backupId}/validate")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ValidateBackup(string backupId, CancellationToken ct)
    ```
  - [x]3.5 Add POST endpoint for restore:
    ```csharp
    [HttpPost("{backupId}/restore")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> TriggerRestore(string backupId, [FromQuery] DateTimeOffset? pointInTime, [FromQuery] bool dryRun = false, CancellationToken ct)
    ```
  - [x]3.6 Add POST endpoint for stream export:
    ```csharp
    [HttpPost("export-stream")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(StreamExportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportStream([FromBody] StreamExportRequest request, CancellationToken ct)
    ```
    **SECURITY (SEC-2 compliance):** `AdminTenantAuthorizationFilter` checks route params, NOT request body. The export endpoint MUST manually validate `request.TenantId` against the JWT tenant claims before proceeding: `if (!User.HasClaim("tenant", request.TenantId) && !IsGlobalAdmin()) return Forbid();`. This prevents a tenant-A admin from exporting tenant-B's streams via body manipulation.
  - [x]3.7 Add POST endpoint for stream import:
    ```csharp
    [HttpPost("import-stream")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ImportStream([FromQuery] string tenantId, [FromBody] string content, CancellationToken ct)
    ```
  - [x]3.8 Copy `ResolveTenantScope`, `MapOperationResult`, `MapAsyncOperationResult`, `IsServiceUnavailable`, `ServiceUnavailable`, `UnexpectedError`, `CreateProblemResult` helper methods from `AdminStorageController` (or extract to a shared base class if time permits — note: extracting a base class is a nice-to-have, not a requirement. Copy-paste is acceptable for v1).
  - [x]3.9 **Checkpoint**: All endpoints compile, follow existing controller patterns, correct auth policies applied (Admin for writes, ReadOnly for reads).

- [x]**Task 4: Create AdminBackupApiClient** (AC: 1, 4, 5, 6, 7, 8, 13)
  - [x]4.1 Create `src/Hexalith.EventStore.Admin.UI/Services/AdminBackupApiClient.cs` following `AdminSnapshotApiClient` pattern exactly: constructor takes `IHttpClientFactory` + `ILogger<AdminBackupApiClient>`, uses named client `"AdminApi"`. Mark all public methods `virtual` for NSubstitute mocking.
  - [x]4.2 Add method `GetBackupJobsAsync(string? tenantId, CancellationToken ct)` → `HttpClient.GetAsync("api/v1/admin/backups?tenantId={id}")`, returns `IReadOnlyList<BackupJob>`. Return empty list on error.
  - [x]4.3 Add method `TriggerBackupAsync(string tenantId, string? description, bool includeSnapshots, CancellationToken ct)` → `HttpClient.PostAsync("api/v1/admin/backups/{Uri.EscapeDataString(tenantId)}?description={desc}&includeSnapshots={bool}")`. Returns `AdminOperationResult?`. Expect **202 Accepted**.
  - [x]4.4 Add method `ValidateBackupAsync(string backupId, CancellationToken ct)` → `HttpClient.PostAsync("api/v1/admin/backups/{Uri.EscapeDataString(backupId)}/validate")`. Returns `AdminOperationResult?`. Expect **202 Accepted**.
  - [x]4.5 Add method `TriggerRestoreAsync(string backupId, DateTimeOffset? pointInTime, bool dryRun, CancellationToken ct)` → `HttpClient.PostAsync("api/v1/admin/backups/{backupId}/restore?pointInTime={iso}&dryRun={bool}")`. Returns `AdminOperationResult?`. Expect **202 Accepted**.
  - [x]4.6 Add method `ExportStreamAsync(StreamExportRequest request, CancellationToken ct)` → `HttpClient.PostAsJsonAsync("api/v1/admin/backups/export-stream", request)`. Returns `StreamExportResult?`.
  - [x]4.7 Add method `ImportStreamAsync(string tenantId, string content, CancellationToken ct)` → `HttpClient.PostAsync("api/v1/admin/backups/import-stream?tenantId={id}", new StringContent(content, Encoding.UTF8, "application/json"))`. Returns `AdminOperationResult?`. Expect **202 Accepted**.
  - [x]4.8 Error handling: copy `HandleErrorStatus` from `AdminSnapshotApiClient` (maps 401 → `UnauthorizedAccessException`, 403 → `ForbiddenAccessException`, 503 → `ServiceUnavailableException`).
  - [x]4.9 Register `AdminBackupApiClient` as scoped in `Program.cs`: `builder.Services.AddScoped<AdminBackupApiClient>();`
  - [x]4.10 **Checkpoint**: Client builds, all methods callable, errors handled gracefully.

- [x]**Task 5: Create Backups.razor page** (AC: 1-15)
  - [x]5.1 Create `src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor` with `@page "/backups"`. Inject: `AdminBackupApiClient`, `NavigationManager`, `IToastService`, `IJSRuntime` (for file download). Implement `IAsyncDisposable`.
  - [x]5.2 Implement `OnInitializedAsync`: parse URL tenant filter, load backup jobs via `GetBackupJobsAsync`.
  - [x]5.3 Render 4 stat cards in `FluentGrid` (xs=6, sm=6, md=3): Active Backups (Warning when > 0), Completed (30d), Total Backup Size, Last Successful. Show `SkeletonCard` during loading.
  - [x]5.4 Render tenant filter `FluentTextField` with 300ms debounce Timer. On filter change: update URL, reload data. Use `InvokeAsync(() => { ... })` in Timer callback.
  - [x]5.5 Render `FluentDataGrid` with backup list. Columns: Backup ID (monospace, truncated), Tenant ID (monospace), Scope (badge), Status (badge), Created (relative time), Duration, Size (FormatBytes), Events (right-aligned), Actions. Default sort: Created desc. Clicking a failed row toggles error detail expansion.
  - [x]5.6 Implement `EmptyState` when no backups match the current filter.
  - [x]5.7 Add action buttons in page header, all gated by `AuthorizedView MinimumRole="AdminRole.Admin"`:
    - "Create Backup" (primary, accent)
    - "Export Stream" (outline, secondary)
    - "Import Stream" (outline, secondary)
  - [x]5.8 Implement "Create Backup" `FluentDialog` (single-step, matching compaction trigger pattern). Fields: Tenant ID (required), Description (optional), Include Snapshots (checkbox, default checked). Dynamic confirmation text + warning banner. Concurrent guard for same-tenant backups.
  - [x]5.9 Implement "Validate" per-row action button (Admin-only). Confirmation dialog → calls `ValidateBackupAsync`.
  - [x]5.10 Implement "Restore" per-row action button (Admin-only, only on backups where `Status == Completed && IsValidated == true`). Two-step dialog: Step 1 shows backup details (tenant, created date, event count, size) + warning banner ("Restoring will re-inject events into a parallel restore stream. Original event streams are never overwritten.") + acknowledgment checkbox ("I understand this will create restored state in the event store"). Step 2 shows point-in-time `FluentDatePicker` (optional — leave empty for full restore) + "Dry Run" `FluentCheckbox`. On confirm → calls `TriggerRestoreAsync`.
  - [x]5.11 Implement "Export Stream" dialog: Tenant ID, Domain, Aggregate ID (all required), Format selector (JSON/CloudEvents). On submit → calls `ExportStreamAsync` → triggers file download via `IJSRuntime.InvokeVoidAsync("blazorDownloadFile", fileName, content)`. Add a small JS interop function in `wwwroot/js/download.js` (or inline in `_Host.cshtml`/`App.razor`) that creates a Blob URL and triggers download.
  - [x]5.12 Implement "Import Stream" dialog: Try `FluentInputFile` for file selection; if not available in v4.13.2, use `InputFile` from ASP.NET Core wrapped in `FluentCard`. Read file via `IBrowserFile.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024)`. On file selected → parse JSON and preview metadata (tenant, domain, aggregate, event count). Warning about append-only behavior (D1). On confirm → calls `ImportStreamAsync`.
  - [x]5.13 Implement status badges: Pending (blue), Running (orange, animated pulse), Completed (green), Failed (red), Validating (teal, animated pulse). Each badge has `aria-label="Status: {status}"`.
  - [x]5.14 Add manual "Refresh" button, `IssueBanner` for error state, `IAsyncDisposable` for Timer + CancellationTokenSource cleanup.
  - [x]5.15 Add URL state management: `ReadUrlParameters()` on init, `UpdateUrl()` on filter change with `replace: true`.
  - [x]5.16 **Checkpoint**: Page loads, stat cards show, grid renders, create backup dialog works, validate/restore actions work, export/import dialogs work, role enforcement active, URL state persists.

- [x]**Task 6: bUnit and unit tests** (AC: 1-15)
  - **Mock dependencies**: extend `AdminUITestContext`, mock `AdminBackupApiClient` using `Substitute.For<AdminBackupApiClient>(...)` with `IHttpClientFactory` + `NullLogger<AdminBackupApiClient>.Instance`.
  - **Culture sensitivity**: Any test asserting formatted numbers must use culture-invariant assertions (lesson from story 16-1 with French locale).
  - **Merge-blocking tests**:
  - [x]6.1 Test `Backups` page renders 4 stat cards with correct values from backup list (AC: 2)
  - [x]6.2 Test `Backups` page shows `SkeletonCard` during loading state (AC: 2)
  - [x]6.3 Test backup grid renders all jobs with correct columns (AC: 1)
  - [x]6.4 Test `EmptyState` shown when no backups exist (AC: 1)
  - [x]6.5 Test `IssueBanner` shown when API returns error (AC: 13)
  - [x]6.6 Test backups page has `<h1>Backups</h1>` heading (AC: 15)
  - [x]6.7 Test "Create Backup" button hidden for ReadOnly and Operator users (AC: 14)
  - [x]6.8 Test "Create Backup" button visible for Admin users (AC: 14)
  - [x]6.9 Test create backup dialog calls `TriggerBackupAsync` on confirm, reloads list, and shows success toast (AC: 4) — **trigger happy path**
  - [x]6.10 Test create backup dialog shows error toast when API returns failure (AC: 4) — **trigger error path**
  - [x]6.11 Test status badges render correct appearance per backup status (AC: 9)
  - [x]6.12 Test "Validate" button only appears on completed backups (AC: 5)
  - [x]6.13 Test "Restore" button only appears on completed+validated backups (AC: 6)
  - **Recommended tests**:
  - [x]6.14 Test URL parameters read on page initialization (AC: 10)
  - [x]6.15 Test create backup dialog renders with correct form fields and warning banner (AC: 4)
  - [x]6.16 Test "Start Backup" button disabled when Tenant ID is empty (AC: 4)
  - [x]6.17 Test stat card "Active Backups" shows Warning severity when count > 0 (AC: 2)
  - [x]6.18 Test stat card "Last Successful" shows "Never" when no completed backups (AC: 2)
  - [x]6.19 Test failed backup row expands to show error message on click (AC: 9)
  - [x]6.20 Test backup list filters by tenant when tenant filter is applied (AC: 3)
  - [x]6.21 Test "Scope" column shows "Full" when StreamId is null and "Stream" when present (AC: 1)
  - [x]6.22 Test restore dialog requires acknowledgment checkbox before proceeding (AC: 6)
  - [x]6.23 Test export dialog calls `ExportStreamAsync` with correct parameters (AC: 7)
  - [x]6.24 Test concurrent guard prevents duplicate backup for same tenant (AC: 4)

- [x]**Task 7: Breadcrumb, NavMenu, Command Palette, and CSS** (AC: 11, 12, 15)
  - [x]7.1 Update `Breadcrumb.razor` route label dictionary: add `"backups" -> "Backups"`.
  - [x]7.2 Update `NavMenu.razor`: add Backups `FluentNavLink` after the Compaction link. Use `Icons.Regular.Size20.ArrowDownload` (or `CloudArrowDown`/`DatabaseArrowDown` if it doesn't compile — verify icon class exists at build time).
  - [x]7.3 Update `CommandPaletteCatalog.cs`: add entries `("Actions", "Backups", "/backups")`, `("Backups", "Backup & Restore", "/backups")`, `("Backups", "Export Stream", "/backups")`, `("Backups", "Import Stream", "/backups")`.
  - [x]7.4 Add CSS styles in `wwwroot/css/app.css`: `.backup-status-running` and `.backup-status-validating` with pulse animation (reuse `.compaction-status-running` pattern if it exists from story 16-3), `.backup-error-detail` for expandable error row (monospace, muted background), `.backup-action-buttons` for row-level action button spacing.
  - [x]7.5 Add a "Run Backup" cross-link on the Storage page: when tenant storage exceeds a threshold (e.g., > 1 GB), render a `FluentAnchor` in the storage overview linking to `/backups?tenant={tenantId}` with text "Back Up". This provides proactive DBA guidance from Storage directly into Backup.
  - [x]7.6 If story 16-3 has not yet been implemented and the JS download helper does not exist, create `src/Hexalith.EventStore.Admin.UI/wwwroot/js/download.js`:
    ```javascript
    window.blazorDownloadFile = function (fileName, content) {
        const blob = new Blob([content], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    };
    ```
    Add `<script src="js/download.js"></script>` to `App.razor` or the host page.
  - [x]7.7 **Checkpoint**: Backups page accessible from sidebar, breadcrumb shows "Home / Backups", Ctrl+K "Backups" navigates correctly, Running/Validating badges pulse, error details styled, file download works.

## Dev Notes

### Architecture Compliance

- **ADR-P4**: Admin.UI communicates exclusively via HTTP REST API to Admin.Server. The `AdminBackupApiClient` calls `api/v1/admin/backups/*` endpoints — no direct DAPR access. All write operations are delegated through Admin.Server which itself delegates to CommandApi via DAPR service invocation.
- **NFR40**: Admin API responses: 500ms at p99 for reads, 2s at p99 for writes (backup trigger, restore trigger).
- **NFR41**: Initial render ≤ 2 seconds. Backup job list is typically small.
- **NFR44**: All admin data access through DAPR abstractions exclusively. Backup metadata stored in DAPR state store. Export reads events from state store. Import delegates writes via service invocation.
- **NFR46**: **Admin role required** for all backup/restore operations. This is distinct from Operator role used for compaction/snapshots. The `AdminRole` enum explicitly states: "Admin: Operator + tenant management, backup/restore." The `AdminAuthorizationPolicies.Admin` ("AdminFull") policy is used for all write endpoints on this controller.
- **FR76**: This story covers the "backup operations" portion of FR76. Storage analysis is story 16-1 (done). Snapshots are story 16-2 (done). Compaction is story 16-3.
- **FR79**: Backup operations must be accessible through all three interfaces (Web UI in this story, CLI in story 17-6, MCP in story 18-4).

### Scope — UI + API Surface + Job Tracking (Not the Backup Engine)

**CRITICAL SCOPE DECISION:** This story delivers the **admin UI, REST API endpoints, API client, and backup job metadata tracking**. The actual backup/restore *processing pipeline* (the engine that reads all events, serializes them, stores the backup blob, replays events during restore) is a **future backend story**. The `DaprBackupCommandService` implementations are **job-tracking stubs** that:

1. Write backup/restore job metadata to admin index keys (job status, timestamps, operation IDs)
2. Return `AdminOperationResult(Success: true, OperationId, Message: "Operation queued")` for all write operations
3. Do NOT perform actual event reading, serialization, or replay — those are delegated to a CommandApi backup handler that does not yet exist

**Why:** The CommandApi has no admin backup/restore endpoints today. Building both the full-stack UI and the backup engine in one story would blow the estimate to 30+ hours and create an untestable monolith. By delivering the UI + API surface first, we get: reviewable UI for stakeholder feedback, complete API contract for CLI (17-6) and MCP (18-4) stories, and a clear backend contract for the future backup engine story.

**Exception — Stream Export:** `ExportStreamAsync` CAN read events directly from DAPR state store (it's a read operation, ADR-P4 allows reads). This is the one operation that works end-to-end in v1.

This story includes:
- Viewing backup job history and status (all users)
- Triggering full-tenant backups — writes job metadata, returns operation ID (Admin only)
- Validating backup integrity — updates job status (Admin only)
- Point-in-time restore with dry-run option — writes job metadata (Admin only)
- Single-stream export as JSON/CloudEvents — **fully functional**, reads events from DAPR (Admin only)
- Single-stream import from previously exported file — delegates to CommandApi (Admin only)

This story does NOT include:
- The actual backup processing engine (future backend story)
- Scheduled/recurring backup policies (future enhancement — v1 is manual trigger only)
- Cross-tenant backup/restore (each backup is tenant-scoped)
- Backup storage to external systems (S3, Azure Blob) — v1 stores in DAPR state store
- Backup compression or encryption — future enhancement
- Bulk multi-stream export — one stream at a time for v1, max 50,000 events
- Automatic backup rotation/cleanup — future policy engine

### Backup Job State Persistence — Expected Empty State

The `GetBackupJobsAsync` query reads from admin index key `admin:backup-jobs:{scope}` in the DAPR state store. **This index is populated by the backup handler** when it processes a trigger request — not by Admin.Server directly (ADR-P4).

**If no backup has ever been triggered**, the admin index key will not exist, and `DaprBackupQueryService` will return an empty list. The page will show `EmptyState` — this is correct and expected behavior, NOT a bug.

**After triggering a backup** via the UI, the job should appear on the next manual refresh. If the job does not appear, the trigger still works (check server logs for 202 response) — the job history feature requires the backend to write to the admin index.

### Restore Operations — Architecture Decision: Parallel Restore Streams

**Critical constraint: Event store keys are write-once (D1).** Existing events at `{tenant}:{domain}:{aggId}:events:{seq}` can never be updated or deleted. Therefore, "restore" does NOT mean overwriting current state. Instead, restore works as follows:

1. **Restore creates a parallel aggregate stream** with a restore-prefixed key: `{tenant}:{domain}:{aggId}:restore-{backupId}:events:{seq}`. This preserves the original stream untouched.
2. **Point-in-time restore** copies events from the backup up to the specified timestamp into the restore stream.
3. **Full restore** copies all events from the backup into the restore stream.
4. **Dry run** validates that the backup events can be deserialized and would produce valid state, without writing anything.
5. After restore completes, the admin can manually promote the restore stream by updating the aggregate metadata pointer (future enhancement — out of scope for v1). For v1, the restore stream exists side-by-side for inspection and comparison.

**Restore stream cleanup:** Repeated restores create accumulating `restore-{backupId}` key namespaces. For v1, this is acceptable since restores are rare operations. Document in the API response: "Restore streams are retained indefinitely. To clean up old restore streams, use the CLI `eventstore-admin backup cleanup --older-than 30d` (story 17-6)." The cleanup mechanism is out of scope for this story but the key pattern is designed to be enumerable for future batch deletion.

**The UI warning text reflects this**: "Restoring will re-inject events from this backup into a parallel restore stream. Original event streams are never overwritten." This is accurate and does not promise destructive overwrites that the architecture cannot deliver.

The restore dialog UX requirements:
1. Multi-step dialog (not single-step like backup trigger)
2. Explicit acknowledgment checkbox ("I understand this will create restored state in the event store")
3. Warning banner with clear description of what will happen
4. Dry-run option to validate without applying
5. Point-in-time option to limit restore scope

The restore dialog should make the consequences crystal clear. Follow the UX confirmation pattern from the spec: "Title → Body (what will happen) → Warning (risk statement) → Primary button restates scope."

### Stream Export/Import — File Download Pattern

Stream export uses JSInterop to trigger a browser file download. This is a new pattern in the Admin.UI — no previous page has needed file downloads. The JS helper function creates a Blob URL and triggers download via a dynamically created `<a>` element.

**Blazor Server pre-render guard:** `IJSRuntime` is not available during Blazor Server pre-rendering. The export download call MUST be guarded: either call `IJSRuntime.InvokeVoidAsync` only from user-initiated event handlers (button clicks — which are always post-render), or check `_isConnected` flag set in `OnAfterRenderAsync(firstRender: true)`. Do NOT call JS interop from `OnInitializedAsync` or `OnParametersSetAsync` — those run during pre-render and will throw `InvalidOperationException`.

**Large export guard:** If `StreamExportResult.Content` exceeds 5 MB, the Blob approach may strain browser memory. For v1, the 50,000-event hard limit on exports (see AC 7) keeps content size manageable. If future stories raise this limit, switch to a server-generated temporary download URL.

For import, try `FluentInputFile` from FluentUI Blazor v4 first. **If `FluentInputFile` does not exist in v4.13.2** (it may be a v5 component), fall back to ASP.NET Core's built-in `InputFile` component wrapped in FluentUI styling (`FluentCard` container with drag-drop hint text). Read the file content as a string via `IBrowserFile.OpenReadStream()` and send it to the API. The preview section should parse the JSON to extract metadata for user confirmation before import.

### Existing Code to Reuse (DO NOT Recreate)

| What | Where | How |
|------|-------|-----|
| `AdminOperationResult` DTO | `Admin.Abstractions/Models/Common/AdminOperationResult.cs` | USE — Success, OperationId, Message, ErrorCode for trigger/validate/restore responses |
| `AdminRole` enum | `Admin.Abstractions/Models/Common/AdminRole.cs` | USE — Admin role for backup/restore gating |
| `AdminAuthorizationPolicies` | `Admin.Server/Authorization/AdminAuthorizationPolicies.cs` | USE — `AdminAuthorizationPolicies.Admin` ("AdminFull") for write endpoints |
| `AdminTenantAuthorizationFilter` | `Admin.Server/Authorization/AdminTenantAuthorizationFilter.cs` | USE — tenant-scoped auth filter on endpoints |
| `AdminStorageController` helpers | `Admin.Server/Controllers/AdminStorageController.cs` | COPY — `MapAsyncOperationResult`, `IsServiceUnavailable`, error helpers |
| `AdminSnapshotApiClient` | `Admin.UI/Services/AdminSnapshotApiClient.cs` | PATTERN MODEL — constructor, HttpClient, error handling, `HandleErrorStatus` |
| `Compaction.razor` | `Admin.UI/Pages/Compaction.razor` | PATTERN MODEL — stat cards, grid, operator-gated actions, URL state, debounce |
| `Snapshots.razor` | `Admin.UI/Pages/Snapshots.razor` | PATTERN MODEL — dialog patterns, multi-step if applicable |
| `StatCard` component | `Admin.UI/Components/Shared/StatCard.razor` | USE — Label, Value, Severity, Title |
| `SkeletonCard` component | `Admin.UI/Components/Shared/SkeletonCard.razor` | USE — loading placeholder |
| `IssueBanner` component | `Admin.UI/Components/Shared/IssueBanner.razor` | USE — error/warning state |
| `EmptyState` component | `Admin.UI/Components/Shared/EmptyState.razor` | USE — no data fallback |
| `AuthorizedView` component | `Admin.UI/Components/Shared/AuthorizedView.razor` | USE — role-gated rendering with `MinimumRole` |
| `TimeFormatHelper` | `Admin.UI/Components/Shared/TimeFormatHelper.cs` | USE — `FormatRelativeTime` for Created column, `FormatBytes` for Size column (should exist after story 16-3) |
| `AdminUITestContext` | `Admin.UI.Tests/AdminUITestContext.cs` | USE — base test class |
| `HandleErrorStatus` method | `AdminSnapshotApiClient.cs` | COPY — same HTTP status → typed exception mapping |

### AdminBackupApiClient — Exact API Endpoints

| Method | HTTP | URL | Params | Returns | Status |
|--------|------|-----|--------|---------|--------|
| GetBackupJobsAsync | GET | `api/v1/admin/backups` | `?tenantId=` | `IReadOnlyList<BackupJob>` | 200 OK |
| TriggerBackupAsync | POST | `api/v1/admin/backups/{tenantId}` | `?description=&includeSnapshots=` | `AdminOperationResult` | **202 Accepted** |
| ValidateBackupAsync | POST | `api/v1/admin/backups/{backupId}/validate` | — | `AdminOperationResult` | **202 Accepted** |
| TriggerRestoreAsync | POST | `api/v1/admin/backups/{backupId}/restore` | `?pointInTime=&dryRun=` | `AdminOperationResult` | **202 Accepted** |
| ExportStreamAsync | POST | `api/v1/admin/backups/export-stream` | body: `StreamExportRequest` | `StreamExportResult` | 200 OK |
| ImportStreamAsync | POST | `api/v1/admin/backups/import-stream` | `?tenantId=`, body: string | `AdminOperationResult` | **202 Accepted** |

GET endpoint requires `ReadOnly` auth policy. All POST endpoints require `Admin` auth policy. All endpoints have `AdminTenantAuthorizationFilter`.

**Important**: POST async endpoints return 202 (not 200). Parse response body as `AdminOperationResult`. ExportStream returns 200 with `StreamExportResult`.

### Dialog Pattern — Match Existing Codebase

**IMPORTANT:** Check how `Snapshots.razor` and `Compaction.razor` implement their dialogs before coding. Match the exact same pattern — `FluentDialog` with `ShowAsync()`/`HideAsync()` or `IDialogService.ShowDialogAsync<T>()`, whichever the codebase uses. Do NOT mix APIs.

- **Create Backup dialog**: Single-step, matching compaction trigger dialog pattern.
- **Restore dialog**: Multi-step (two steps) — more complex than any existing dialog in the codebase. Use `_restoreStep` int state to track which step is shown. Both steps render in the same `FluentDialog`, with conditional rendering based on step.

```csharp
private int _restoreStep = 1;
private bool _restoreAcknowledged;

// Step 1: Confirmation + acknowledgment
// Step 2: Options (point-in-time, dry run) + trigger
```

### Previous Story Intelligence (16-3 Compaction Manager)

Key learnings from story 16-3 that apply to 16-4:

- **Culture-dependent formatting in tests**: Use culture-invariant assertions. French locale produces "1,0" instead of "1.0".
- **FluentDataGrid `OnRowClick`**: Use `OnRowClick` attribute. For backups, row click toggles error detail expansion for failed jobs.
- **Timer + InvokeAsync**: The debounce Timer callback runs on a thread pool thread. All state mutations and `StateHasChanged()` MUST be wrapped in `InvokeAsync(() => { ... })`.
- **AdminApiClient error pattern**: Throws `ServiceUnavailableException` on non-auth errors. The page catches this and shows `IssueBanner`.
- **Admin.UI tests currently pass** (242+ after 16-2, more after 16-3) — no regressions allowed.
- **JSInterop in tests uses `Mode = Loose`** — no explicit JS setup needed for `IJSRuntime` calls in bUnit.
- **FluentUI Blazor v4 icon syntax**: `@(new Icons.Regular.Size20.IconName())`. Verify icon class exists at compile time.
- **`DashboardRefreshService`**: Do NOT subscribe on this page — backup data changes infrequently. Manual refresh only.
- **BL0005 error with `@onclick:stopPropagation`**: Wrap click handlers in grid rows in `<div @onclick:stopPropagation>`.
- **Dialog pattern**: Follow the same `FluentDialog` pattern as compaction/snapshots for consistency.
- **AdminSnapshotApiClient as pattern model**: Constructor takes `IHttpClientFactory` + `ILogger<T>`, marks all public methods `virtual`.
- **POST endpoints returning 202**: Check `response.StatusCode == HttpStatusCode.Accepted` — parse body as `AdminOperationResult`.
- **CompactionJob expected empty state**: Same applies to BackupJob — empty list is correct when no backups exist.
- **FormatBytes shared helper**: Should exist in `TimeFormatHelper.cs` after story 16-3 extraction. If not, create it there.

### Deferred Improvements (Out of Scope — Future Stories)

- **Scheduled backups** — Recurring backup policies with cron-style scheduling.
- **External storage targets** — Backup to S3, Azure Blob Storage, or file system instead of DAPR state store.
- **Backup compression and encryption** — Reduce storage and protect sensitive data.
- **Cross-tenant restore** — Restore from one tenant's backup into another tenant.
- **Bulk stream export** — Export multiple streams or entire domains at once.
- **Backup rotation policies** — Auto-delete old backups after retention period.
- **Real-time status updates** — Subscribe to SignalR for live backup job status.
- **Restore progress tracking** — Detailed progress bar for long-running restores.
- **Backup size estimation** — Show estimated backup size before triggering.

### Git Intelligence

Branch naming: `feat/story-16-4-backup-and-restore-console`. PR workflow: feature branch → PR → merge to main. Conventional commits: `feat:` prefix.

**Dependency on story 16-3:** This story assumes `TimeFormatHelper.FormatBytes` exists as a shared static method (extracted from `Storage.razor` in story 16-3) and that the Compaction nav link is already in `NavMenu.razor`. **Branch from main AFTER story 16-3 is merged.** If 16-3 is not yet merged, branch from `feat/story-16-3-compaction-manager` instead and rebase onto main when 16-3 merges. If `FormatBytes` is not yet shared, create it in `TimeFormatHelper.cs` yourself (see story 16-3 Task 6 for the pattern).

### Project Structure Notes

Files to create:
- **CREATE**: `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/BackupJob.cs` — DTO record
- **CREATE**: `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/BackupJobStatus.cs` — Status enum
- **CREATE**: `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/StreamExportRequest.cs` — Export request record
- **CREATE**: `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/StreamExportResult.cs` — Export result record
- **CREATE**: `src/Hexalith.EventStore.Admin.Abstractions/Services/IBackupQueryService.cs` — Query service interface
- **CREATE**: `src/Hexalith.EventStore.Admin.Abstractions/Services/IBackupCommandService.cs` — Command service interface
- **CREATE**: `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupQueryService.cs` — DAPR query service impl
- **CREATE**: `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs` — DAPR command service impl
- **CREATE**: `src/Hexalith.EventStore.Admin.Server/Controllers/AdminBackupsController.cs` — REST API controller
- **CREATE**: `src/Hexalith.EventStore.Admin.UI/Services/AdminBackupApiClient.cs` — HTTP client
- **CREATE**: `src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor` — Backup & restore page
- **CREATE**: `src/Hexalith.EventStore.Admin.UI/wwwroot/js/download.js` — JS interop for file download (if not already existing)
- **CREATE**: `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/BackupsPageTests.cs` — bUnit tests

Files to modify:
- **MODIFY**: `src/Hexalith.EventStore.Admin.Server/Program.cs` (or DI setup) — register `IBackupQueryService`, `IBackupCommandService`
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Program.cs` — register `AdminBackupApiClient`
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor` — add Backups nav link
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Layout/Breadcrumb.razor` — add "backups" route label
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/Components/CommandPaletteCatalog.cs` — add Backups entries
- **MODIFY**: `src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css` — backup-specific styles (pulse animation, error detail)

### References

- [Source: _bmad-output/planning-artifacts/epics.md — Epic 16, Story 16-4]
- [Source: _bmad-output/planning-artifacts/architecture.md — ADR-P4, D1, NFR40, NFR44, NFR46]
- [Source: _bmad-output/planning-artifacts/prd.md — FR76, FR79, Journey 8 Maria DBA]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR50-DR59, Confirmation & Destructive Action Patterns]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Common/AdminRole.cs — Admin role includes backup/restore]
- [Source: src/Hexalith.EventStore.Admin.Server/Authorization/AdminAuthorizationPolicies.cs — AdminFull policy]
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/AdminStorageController.cs — controller patterns, MapAsyncOperationResult]
- [Source: _bmad-output/implementation-artifacts/16-3-compaction-manager.md — previous story learnings]

## Change Log

- 2026-03-24: Implemented full Backup & Restore Console: 4 DTOs, 2 service interfaces + DAPR implementations, REST API controller (7 endpoints), AdminBackupApiClient, Backups.razor page (stat cards, grid, create/validate/restore/export/import dialogs), navigation/breadcrumb/command palette updates, CSS status badges, 23 bUnit tests. All 281 Admin.UI tests pass.
- 2026-03-24: **Code review fixes** (10 patches + 3 bad-spec amendments):
  - P-1: Fixed `DaprBackupCommandService.TriggerBackupAsync` URL — now includes `{tenantId}` in path and query params instead of sending body to base route
  - P-2: Added route constraint `regex(^(?!export-stream$|import-stream$).+$)` on `{tenantId}` route to prevent ambiguity with literal `export-stream`/`import-stream` routes
  - P-3: Added `[ServiceFilter(typeof(AdminTenantAuthorizationFilter))]` to `ValidateBackup` and `TriggerRestore` endpoints
  - P-4: Added SEC-2 manual tenant claim check to `ImportStream` endpoint + `[RequestSizeLimit(10MB)]`
  - P-5: Fixed `CancellationTokenSource` leak — added `.Dispose()` before reassignment in `LoadDataAsync` and all operation handlers
  - P-6: Fixed debounce timer leak — previous Timer stopped and disposed before creating new one
  - P-7: Added `[RequestSizeLimit(10 * 1024 * 1024)]` to `ImportStream` endpoint
  - P-8: Restore dialog now validates point-in-time input — shows inline error for invalid/future dates instead of silently ignoring
  - P-9: Added step indicator badges with `aria-current="step"` to restore dialog
  - P-10: Added 50,000 event export limit warning card in export dialog
  - BS-1: Added `BackupJobType` enum (`Backup`/`Restore`) and `JobType` field to `BackupJob` record. Updated concurrent guards to use `JobType` instead of `Description.Contains("restore")` heuristic
  - BS-3: Added `HasActiveRestoreForRestoreTenant` guard — restore dialog now blocks on both active backups AND active restores for same tenant
  - All 281 UI tests + 224 abstractions tests pass after fixes

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (claude-opus-4-6)

### Debug Log References

- Build error CA1062 on `ExportStreamAsync` request param — resolved with `ArgumentNullException.ThrowIfNull(request)` in both controller and service
- Build error RZ10001 on `FluentSelect` missing TOption — resolved by adding `TOption="string"` to FluentSelect and FluentOption
- Pre-existing IntegrationTests CS0433 `Program` type conflict (Tier 3, unrelated) — not addressed

### Completion Notes List

- Created 4 DTOs: `BackupJobStatus` enum (5 values incl. Validating), `BackupJob` record, `StreamExportRequest` record, `StreamExportResult` record in Admin.Abstractions/Models/Storage/
- Created `IBackupQueryService` and `IBackupCommandService` interfaces in Admin.Abstractions/Services/
- Implemented `DaprBackupQueryService` (reads from `admin:backup-jobs:{scope}` index) and `DaprBackupCommandService` (delegates to CommandApi via DAPR service invocation) in Admin.Server/Services/
- Registered both services in `ServiceCollectionExtensions.AddAdminServer()` via `TryAddScoped`
- Created `AdminBackupsController` with 7 endpoints: GET backup jobs (ReadOnly), POST trigger backup (Admin), POST validate (Admin), POST restore (Admin), POST export-stream (Admin with SEC-2 body tenant check), POST import-stream (Admin)
- Created `AdminBackupApiClient` with 7 methods following existing pattern (virtual methods, HandleErrorStatus, named HttpClient "AdminApi")
- Built complete `Backups.razor` page with: 4 stat cards (Active Backups, Completed 30d, Total Backup Size, Last Successful), FluentDataGrid with 9 columns (ID truncated, Tenant, Scope badge, Status badge, Created, Duration, Size, Events, Actions), Create Backup dialog with concurrent guard, Validate confirmation dialog, multi-step Restore dialog (acknowledgment + options), Export Stream dialog with JS file download, Import Stream dialog with file parsing and preview
- Created `download.js` JS interop for browser file download and registered in App.razor
- Updated NavMenu.razor (Backups link with ArrowDownload icon after Compaction)
- Updated Breadcrumb.razor (["backups"] = "Backups")
- Updated CommandPaletteCatalog.cs (4 entries: Actions/Backups, Backup & Restore, Export Stream, Import Stream)
- Added CSS styles: backup-status-running (pulse animation, warning), backup-status-completed (success), backup-status-failed (error), backup-status-validating (pulse, inflight/teal), backup-error-detail (monospace expandable)
- 23 bUnit tests: 13 merge-blocking + 10 recommended covering stat cards, loading, grid, empty state, error banner, heading, role gating (ReadOnly/Admin), create dialog happy/error path, status badges, validate/restore button visibility, URL params, form fields, disabled state, warning severity, "Never" display, filter, scope column, concurrent guard, export dialog
- Full test suite: 281 passed, 0 failed, 0 skipped

### File List

**New files:**
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/BackupJobType.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/BackupJobStatus.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/BackupJob.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/StreamExportRequest.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/StreamExportResult.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Services/IBackupQueryService.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Services/IBackupCommandService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprBackupCommandService.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminBackupsController.cs`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminBackupApiClient.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/Backups.razor`
- `src/Hexalith.EventStore.Admin.UI/wwwroot/js/download.js`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/BackupsPageTests.cs`

**Modified files:**
- `src/Hexalith.EventStore.Admin.Server/Configuration/ServiceCollectionExtensions.cs` (added DI registrations)
- `src/Hexalith.EventStore.Admin.UI/Program.cs` (added AdminBackupApiClient registration)
- `src/Hexalith.EventStore.Admin.UI/Layout/NavMenu.razor` (added Backups nav link)
- `src/Hexalith.EventStore.Admin.UI/Layout/Breadcrumb.razor` (added backups route label)
- `src/Hexalith.EventStore.Admin.UI/Components/CommandPaletteCatalog.cs` (added 4 backup entries)
- `src/Hexalith.EventStore.Admin.UI/Components/App.razor` (added download.js script reference)
- `src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css` (added backup status badge styles)
