# Story 17.6: Snapshot and Backup Subcommands

Status: done

Size: Large — 14 new source files (6 snapshot, 8 backup), 10 test files, 2 modified files (AdminApiClient, Program.cs), 7 task groups, 16 ACs, ~40 tests (~12-15 hours estimated). Replaces the `snapshot` and `backup` stubs from story 17-1 with ten sub-subcommands that call the existing Admin API storage and backup endpoints. Reuses output formatting, `AdminApiClient`, `GlobalOptionsBinding`, and exit code infrastructure from 17-1. Adds `PutAsync` and `DeleteAsync` HTTP methods to `AdminApiClient`.

**Dependency:** Story 17-1 must be complete (done). This story builds on the CLI scaffold, global options, output formatting, `AdminApiClient`, and exit code conventions established there. Stories 17-2 through 17-5 patterns are followed but their completion is not a prerequisite.

## Definition of Done

- All 16 ACs verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- All `snapshot` sub-subcommands return formatted output with correct exit codes
- All `backup` sub-subcommands return formatted output with correct exit codes
- `AdminApiClient` supports PUT and DELETE HTTP methods

## Story

As a **platform operator or DBA**,
I want **`eventstore-admin snapshot` and `eventstore-admin backup` sub-subcommands to manage snapshot policies, create on-demand snapshots, trigger and monitor backups, validate integrity, restore from backups, and export/import streams**,
so that **I can perform storage management and disaster recovery operations from the CLI without needing the Web UI, enabling scripted backup workflows and CI/CD-integrated snapshot policy management (FR13, FR76, FR79, FR80, NFR42)**.

## Acceptance Criteria

1. **`snapshot policies` subcommand** — `eventstore-admin snapshot policies [--tenant <tenantId>]` calls `GET /api/v1/admin/storage/snapshot-policies?tenantId=<tenantId>` and displays a table of `SnapshotPolicy` records. Columns: Tenant ID (TenantId), Domain (Domain), Aggregate Type (AggregateType), Interval (IntervalEvents, right-aligned), Created (CreatedAtUtc). `--tenant` / `-T` is an optional filter (alias `-T` since `-t` is taken by `--token`). Exit code `0` on success, `2` on error. Empty result set returns exit code `0` with "No snapshot policies found." to stderr. JSON format: serialize the entire list. CSV format: same columns as table.

2. **`snapshot create` subcommand** — `eventstore-admin snapshot create <tenantId> <domain> <aggregateId>` calls `POST /api/v1/admin/storage/{tenantId}/{domain}/{aggregateId}/snapshot` and displays the `AdminOperationResult`. For `table` format: key/value pairs: Operation ID (OperationId), Success (Success), Message (Message). Exit code `0` on success, `2` on error. HTTP 403 prints "Access denied. Operator role required." to stderr, exit code `2`. HTTP 404 prints "Aggregate not found." to stderr, exit code `2`. JSON format: full `AdminOperationResult` serialized.

3. **`snapshot set-policy` subcommand** — `eventstore-admin snapshot set-policy <tenantId> <domain> <aggregateType> <intervalEvents>` calls `PUT /api/v1/admin/storage/{tenantId}/{domain}/{aggregateType}/snapshot-policy?intervalEvents=<N>` and displays the `AdminOperationResult`. `intervalEvents` is a positional `Argument<int>` with description "Number of events between automatic snapshots". Exit code `0` on success, `2` on error. HTTP 403: "Access denied. Operator role required." JSON format: full `AdminOperationResult` serialized.

4. **`snapshot delete-policy` subcommand** — `eventstore-admin snapshot delete-policy <tenantId> <domain> <aggregateType>` calls `DELETE /api/v1/admin/storage/{tenantId}/{domain}/{aggregateType}/snapshot-policy` and displays the `AdminOperationResult`. Exit code `0` on success, `2` on error. HTTP 404 prints "Snapshot policy not found." to stderr, exit code `2`. JSON format: full `AdminOperationResult` serialized.

5. **Parent `snapshot` command help** — Running `eventstore-admin snapshot` with no subcommand prints help listing all four sub-subcommands (`policies`, `create`, `set-policy`, `delete-policy`) with their descriptions. `System.CommandLine` provides this automatically for parent commands with no handler.

6. **`backup list` subcommand** — `eventstore-admin backup list [--tenant <tenantId>]` calls `GET /api/v1/admin/backups?tenantId=<tenantId>` and displays a table of `BackupJob` records. Columns: Backup ID (BackupId), Tenant (TenantId), Type (JobType), Status (Status), Snapshots (IncludeSnapshots), Events (EventCount, right-aligned), Size (SizeBytes, right-aligned), Created (CreatedAtUtc). `--tenant` / `-T` optional filter. Exit code `0` on success, `2` on error. Empty result set returns exit code `0` with "No backup jobs found." to stderr. JSON format: serialize the entire list. CSV format: same columns as table.

7. **`backup trigger` subcommand** — `eventstore-admin backup trigger <tenantId> [--description <text>] [--no-snapshots]` calls `POST /api/v1/admin/backups/{tenantId}?description=<text>&includeSnapshots=<bool>` and displays the `AdminOperationResult`. `--description` / `-d` is `Option<string?>` (optional). `--no-snapshots` is `Option<bool>` (default false) — when present, passes `includeSnapshots=false` to the API (default is true = include snapshots). For `table` format: key/value pairs: Operation ID (OperationId), Success (Success), Message (Message). For `json` format: full `AdminOperationResult` serialized. Prints "Backup triggered. Operation ID: {id}" to stderr on success. Exit code `0` on success (202 Accepted), `2` on error. HTTP 403: "Access denied. Admin role required."

8. **`backup validate` subcommand** — `eventstore-admin backup validate <backupId>` calls `POST /api/v1/admin/backups/{backupId}/validate` and displays the `AdminOperationResult`. Positional `Argument<string>("backupId", "Backup identifier")`. For `table` format: key/value pairs. For `json` format: full `AdminOperationResult` serialized. Exit code `0` on success, `2` on error. HTTP 404: "Backup '{backupId}' not found." to stderr.

9. **`backup restore` subcommand** — `eventstore-admin backup restore <backupId> [--point-in-time <datetime>] [--dry-run]` calls `POST /api/v1/admin/backups/{backupId}/restore?pointInTime=<datetime>&dryRun=<bool>` and displays the `AdminOperationResult`. `--point-in-time` is `Option<DateTimeOffset?>` (optional ISO 8601 datetime). `--dry-run` is `Option<bool>` (default false) — validates restore feasibility without writing. For `table` format: key/value pairs. For `json` format: full `AdminOperationResult`. Prints "Restore initiated. Operation ID: {id}" to stderr on success (or "Dry-run restore validated." when `--dry-run` is true). Exit code `0` on success, `2` on error.

10. **`backup export-stream` subcommand** — `eventstore-admin backup export-stream <tenantId> <domain> <aggregateId> [--export-format JSON|CloudEvents]` calls `POST /api/v1/admin/backups/export-stream` with JSON body `{ "tenantId": "...", "domain": "...", "aggregateId": "...", "format": "JSON" }` and displays the `StreamExportResult`. `--export-format` is `Option<string>` (default "JSON", accepts "JSON" or "CloudEvents") — named `--export-format` to avoid conflict with global `--format`. For `table` format: key/value summary: Success (Success), Tenant (TenantId), Domain (Domain), Aggregate (AggregateId), Events (EventCount), File Name (FileName). For `json` format: full `StreamExportResult` serialized (includes content). Exit code `0` on success, `2` on error. When `StreamExportResult.Success` is false: print error message to stderr, exit code `2`.

11. **`backup import-stream` subcommand** — `eventstore-admin backup import-stream <tenantId> --file <path>` reads file content from `--file` and calls `POST /api/v1/admin/backups/import-stream?tenantId=<tenantId>` with the file content as the JSON body. `--file` / `-f` is `Option<string>` (required). For `table` format: key/value pairs from `AdminOperationResult`. For `json` format: full `AdminOperationResult` serialized. Exit code `0` on success, `2` on error. If `--file` path does not exist: print "File not found: {path}" to stderr, exit code `2`. Note: HTTP 413 (payload too large) is not explicitly handled by `AdminApiClient` — it falls through to `EnsureSuccessStatusCode()` which produces a generic `HttpRequestException`. The server enforces a 10MB `[RequestSizeLimit]`; with JSON string encoding overhead, practical import ceiling is ~8MB of raw content. If the server rejects the payload, the generic error message will surface.

12. **Parent `backup` command help** — Running `eventstore-admin backup` with no subcommand prints help listing all six sub-subcommands (`list`, `trigger`, `validate`, `restore`, `export-stream`, `import-stream`) with their descriptions.

13. **AdminApiClient PUT and DELETE methods** — Add `PutAsync<TResponse>(string path, CancellationToken cancellationToken)` and `DeleteAsync<TResponse>(string path, CancellationToken cancellationToken)` methods to `AdminApiClient`. Both follow the same error handling pattern as `PostAsync`: connection/timeout/JSON errors wrapped as `AdminApiException`, HTTP 401/403/404/5xx mapped to descriptive messages, 200 and 202 treated as success. Used by `snapshot set-policy` (PUT) and `snapshot delete-policy` (DELETE).

14. **Positional arguments** — All snapshot sub-subcommands requiring stream identity use three positional arguments: `tenantId` (`Argument<string>`), `domain` (`Argument<string>`), `aggregateId` or `aggregateType` (`Argument<string>`). `snapshot set-policy` adds a fourth positional: `intervalEvents` (`Argument<int>`). Backup sub-subcommands use `tenantId` or `backupId` as positional arguments. `backup export-stream` uses three positional arguments (tenantId, domain, aggregateId). Missing required arguments produce `System.CommandLine`'s built-in error message and usage help.

15. **Error handling** — All sub-subcommands reuse `AdminApiClient` error handling from story 17-1 for HTTP 401/403/5xx, connection refused, timeout, and JSON deserialization failures. Write operations (create, set-policy, delete-policy, trigger, validate, restore, export-stream, import-stream) use `PostAsync<AdminOperationResult>`, `PutAsync<AdminOperationResult>`, or `DeleteAsync<AdminOperationResult>` which throw `AdminApiException` on errors. Catches at the command level with specific per-status messages to stderr, exit code `2`. Read operations (policies, list) use `GetAsync<T>` or `TryGetAsync<T>`.

16. **Test coverage** — Unit tests cover: each sub-subcommand's output formatting (table, JSON, CSV where applicable), exit code mapping (success/error), HTTP error handling (401, 403, 404), positional argument parsing, enum serialization as strings, AdminApiClient PUT/DELETE methods (success, error status codes, connection errors), URL encoding of special characters in path parameters, `--tenant` filter passing, `--no-snapshots` flag inversion, `--dry-run` flag, `--point-in-time` parsing, `--export-format` passing, `--file` reading and error on missing file, `--description` passing, empty list handling. All numeric values output as raw numbers (consistent with existing CLI commands).

## Tasks / Subtasks

- [x] **Task 1: Extend AdminApiClient with PUT and DELETE** (AC: 13)
  - [x] 1.1 Add `PutAsync<TResponse>` to `Client/AdminApiClient.cs`:
    ```csharp
    /// <summary>
    /// Sends a PUT request with no body and deserializes the JSON response.
    /// Treats both HTTP 200 and 202 as success.
    /// </summary>
    /// <exception cref="AdminApiException">Thrown for all API communication errors.</exception>
    public async Task<TResponse> PutAsync<TResponse>(string path, CancellationToken cancellationToken)
    {
        string resolvedUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "unknown";
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PutAsync(path, null, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            throw new AdminApiException($"Cannot connect to Admin API at {resolvedUrl}. Is the server running?", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AdminApiException($"Cannot connect to Admin API at {resolvedUrl}. Is the server running?", ex);
        }
        catch (TaskCanceledException ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new AdminApiException($"Request was canceled. (URL: {resolvedUrl})", ex);
            }
            throw new AdminApiException($"Request timed out after 10 seconds. (URL: {resolvedUrl})", ex);
        }

        using (response)
        {
            int statusCode = (int)response.StatusCode;
            switch (statusCode)
            {
                case 401:
                    throw new AdminApiException($"Authentication required. Use --token to provide a JWT token. (URL: {resolvedUrl})", 401);
                case 403:
                    throw new AdminApiException($"Access denied. Insufficient permissions. (URL: {resolvedUrl})", 403);
                case 404:
                    throw new AdminApiException($"Resource not found at {resolvedUrl}{path}.", 404);
                case >= 500:
                    throw new AdminApiException($"Admin API server error: {statusCode}. (URL: {resolvedUrl})", statusCode);
            }

            if (statusCode is not (200 or 202))
            {
                _ = response.EnsureSuccessStatusCode();
            }

            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return JsonSerializer.Deserialize<TResponse>(json, JsonDefaults.Options)
                    ?? throw new AdminApiException("Invalid response from Admin API. Possible version mismatch between CLI and server.");
            }
            catch (JsonException ex)
            {
                throw new AdminApiException("Invalid response from Admin API. Possible version mismatch between CLI and server.", ex);
            }
        }
    }
    ```
  - [x] 1.2 Add `DeleteAsync<TResponse>` to `Client/AdminApiClient.cs`:
    ```csharp
    /// <summary>
    /// Sends a DELETE request and deserializes the JSON response.
    /// Treats both HTTP 200 and 202 as success.
    /// </summary>
    /// <exception cref="AdminApiException">Thrown for all API communication errors.</exception>
    public async Task<TResponse> DeleteAsync<TResponse>(string path, CancellationToken cancellationToken)
    {
        string resolvedUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "unknown";
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            throw new AdminApiException($"Cannot connect to Admin API at {resolvedUrl}. Is the server running?", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AdminApiException($"Cannot connect to Admin API at {resolvedUrl}. Is the server running?", ex);
        }
        catch (TaskCanceledException ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new AdminApiException($"Request was canceled. (URL: {resolvedUrl})", ex);
            }
            throw new AdminApiException($"Request timed out after 10 seconds. (URL: {resolvedUrl})", ex);
        }

        using (response)
        {
            int statusCode = (int)response.StatusCode;
            switch (statusCode)
            {
                case 401:
                    throw new AdminApiException($"Authentication required. Use --token to provide a JWT token. (URL: {resolvedUrl})", 401);
                case 403:
                    throw new AdminApiException($"Access denied. Insufficient permissions. (URL: {resolvedUrl})", 403);
                case 404:
                    throw new AdminApiException($"Resource not found at {resolvedUrl}{path}.", 404);
                case >= 500:
                    throw new AdminApiException($"Admin API server error: {statusCode}. (URL: {resolvedUrl})", statusCode);
            }

            if (statusCode is not (200 or 202))
            {
                _ = response.EnsureSuccessStatusCode();
            }

            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return JsonSerializer.Deserialize<TResponse>(json, JsonDefaults.Options)
                    ?? throw new AdminApiException("Invalid response from Admin API. Possible version mismatch between CLI and server.");
            }
            catch (JsonException ex)
            {
                throw new AdminApiException("Invalid response from Admin API. Possible version mismatch between CLI and server.", ex);
            }
        }
    }
    ```
  - [x] 1.3 Add `using System.Text.Json;` if not already present (it is — no change needed).

- [x] **Task 2: Create snapshot parent command and shared arguments** (AC: 5, 14)
  - [x] 2.1 Create `Commands/Snapshot/SnapshotCommand.cs` — parent command with no handler:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli.Commands.Snapshot;
    public static class SnapshotCommand
    {
        public static Command Create(GlobalOptionsBinding binding)
        {
            Command command = new("snapshot", "Manage aggregate snapshots and snapshot policies");
            command.Subcommands.Add(SnapshotPoliciesCommand.Create(binding));
            command.Subcommands.Add(SnapshotCreateCommand.Create(binding));
            command.Subcommands.Add(SnapshotSetPolicyCommand.Create(binding));
            command.Subcommands.Add(SnapshotDeletePolicyCommand.Create(binding));
            return command;
        }
    }
    ```
  - [x] 2.2 Create `Commands/Snapshot/SnapshotArguments.cs` — shared positional arguments:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli.Commands.Snapshot;
    public static class SnapshotArguments
    {
        public static Argument<string> TenantId() => new("tenantId", "Tenant identifier");
        public static Argument<string> Domain() => new("domain", "Domain name");
        public static Argument<string> AggregateId() => new("aggregateId", "Aggregate identifier");
        public static Argument<string> AggregateType() => new("aggregateType", "Aggregate type name");
    }
    ```
  - [x] 2.3 Update `Program.cs` — replace `StubCommands.Create("snapshot", ...)` with `SnapshotCommand.Create(binding)`. Add `using Hexalith.EventStore.Admin.Cli.Commands.Snapshot;`. Keep all other stubs unchanged.

- [x] **Task 3: Snapshot sub-subcommands** (AC: 1, 2, 3, 4)
  - [x] 3.1 Create `Commands/Snapshot/SnapshotPoliciesCommand.cs`:
    - Optional `--tenant` / `-T` filter option (`Option<string?>`).
    - Handler: calls `GetAsync<List<SnapshotPolicy>>("api/v1/admin/storage/snapshot-policies")` or `GetAsync<List<SnapshotPolicy>>("api/v1/admin/storage/snapshot-policies?tenantId={tenantId}")` when filter provided.
    - Column definitions:
    ```csharp
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Tenant ID", "TenantId"),
        new("Domain", "Domain"),
        new("Aggregate Type", "AggregateType"),
        new("Interval", "IntervalEvents", Align: Alignment.Right),
        new("Created", "CreatedAtUtc"),
    ];
    ```
    - When list is empty: print "No snapshot policies found." to stderr, exit code `0`.
    - Formats via `FormatCollection` with `Columns`.
    - Testable overload:
    ```csharp
    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string? tenantId,
        CancellationToken cancellationToken)
    ```
  - [x] 3.2 Create `Commands/Snapshot/SnapshotCreateCommand.cs`:
    - Three positional arguments: `SnapshotArguments.TenantId()`, `SnapshotArguments.Domain()`, `SnapshotArguments.AggregateId()`.
    - Handler: calls `PostAsync<AdminOperationResult>("api/v1/admin/storage/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateId)}/snapshot", cancellationToken)`.
    - Column definitions for result display:
    ```csharp
    internal static readonly List<ColumnDefinition> ResultColumns =
    [
        new("Operation ID", "OperationId"),
        new("Success", "Success"),
        new("Message", "Message"),
    ];
    ```
    - On success: print "Snapshot created. Operation ID: {id}" to stderr.
    - On `AdminApiException` with status 403: "Access denied. Operator role required." to stderr, exit code `2`.
    - On `AdminApiException` with status 404: "Aggregate not found." to stderr, exit code `2`.
    - Testable overload:
    ```csharp
    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenantId,
        string domain,
        string aggregateId,
        CancellationToken cancellationToken)
    ```
  - [x] 3.3 Create `Commands/Snapshot/SnapshotSetPolicyCommand.cs`:
    - Four positional arguments: `SnapshotArguments.TenantId()`, `SnapshotArguments.Domain()`, `SnapshotArguments.AggregateType()`, `Argument<int>("intervalEvents", "Number of events between automatic snapshots")`.
    - Handler: calls `PutAsync<AdminOperationResult>($"api/v1/admin/storage/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateType)}/snapshot-policy?intervalEvents={intervalEvents}", cancellationToken)`.
    - Same `ResultColumns` as create.
    - On success: print "Snapshot policy set. Operation ID: {id}" to stderr.
    - On `AdminApiException` with status 403: "Access denied. Operator role required." to stderr.
    - Testable overload:
    ```csharp
    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenantId,
        string domain,
        string aggregateType,
        int intervalEvents,
        CancellationToken cancellationToken)
    ```
  - [x] 3.4 Create `Commands/Snapshot/SnapshotDeletePolicyCommand.cs`:
    - Three positional arguments: `SnapshotArguments.TenantId()`, `SnapshotArguments.Domain()`, `SnapshotArguments.AggregateType()`.
    - Handler: calls `DeleteAsync<AdminOperationResult>($"api/v1/admin/storage/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateType)}/snapshot-policy", cancellationToken)`.
    - Same `ResultColumns` as create.
    - On success: print "Snapshot policy deleted. Operation ID: {id}" to stderr.
    - On `AdminApiException` with status 404: "Snapshot policy not found." to stderr, exit code `2`.
    - Testable overload:
    ```csharp
    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenantId,
        string domain,
        string aggregateType,
        CancellationToken cancellationToken)
    ```

- [x] **Task 4: Create backup parent command and shared arguments** (AC: 12, 14)
  - [x] 4.1 Create `Commands/Backup/BackupCommand.cs` — parent command with no handler:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli.Commands.Backup;
    public static class BackupCommand
    {
        public static Command Create(GlobalOptionsBinding binding)
        {
            Command command = new("backup", "Trigger and manage backups, export and import streams");
            command.Subcommands.Add(BackupListCommand.Create(binding));
            command.Subcommands.Add(BackupTriggerCommand.Create(binding));
            command.Subcommands.Add(BackupValidateCommand.Create(binding));
            command.Subcommands.Add(BackupRestoreCommand.Create(binding));
            command.Subcommands.Add(BackupExportStreamCommand.Create(binding));
            command.Subcommands.Add(BackupImportStreamCommand.Create(binding));
            return command;
        }
    }
    ```
  - [x] 4.2 Create `Commands/Backup/BackupArguments.cs` — shared positional arguments:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli.Commands.Backup;
    public static class BackupArguments
    {
        public static Argument<string> TenantId() => new("tenantId", "Tenant identifier");
        public static Argument<string> BackupId() => new("backupId", "Backup identifier");
        public static Argument<string> Domain() => new("domain", "Domain name");
        public static Argument<string> AggregateId() => new("aggregateId", "Aggregate identifier");
    }
    ```
  - [x] 4.3 Update `Program.cs` — replace `StubCommands.Create("backup", ...)` with `BackupCommand.Create(binding)`. Add `using Hexalith.EventStore.Admin.Cli.Commands.Backup;`.

- [x] **Task 5: Backup list and trigger sub-subcommands** (AC: 6, 7)
  - [x] 5.1 Create `Commands/Backup/BackupListCommand.cs`:
    - Optional `--tenant` / `-T` filter option (`Option<string?>`).
    - Handler: calls `GetAsync<List<BackupJob>>("api/v1/admin/backups")` or `GetAsync<List<BackupJob>>("api/v1/admin/backups?tenantId={tenantId}")` when filter provided.
    - Column definitions:
    ```csharp
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Backup ID", "BackupId"),
        new("Tenant", "TenantId"),
        new("Type", "JobType"),
        new("Status", "Status"),
        new("Snapshots", "IncludeSnapshots"),
        new("Events", "EventCount", Align: Alignment.Right),
        new("Size", "SizeBytes", Align: Alignment.Right),
        new("Created", "CreatedAtUtc"),
    ];
    ```
    - When list is empty: print "No backup jobs found." to stderr, exit code `0`.
    - Formats via `FormatCollection` with `Columns`.
    - Testable overload:
    ```csharp
    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string? tenantId,
        CancellationToken cancellationToken)
    ```
  - [x] 5.2 Create `Commands/Backup/BackupTriggerCommand.cs`:
    - One positional argument: `BackupArguments.TenantId()`.
    - Options: `--description` / `-d` (`Option<string?>`, optional), `--no-snapshots` (`Option<bool>`, default false).
    - Handler: builds query string:
    ```csharp
    string path = $"api/v1/admin/backups/{Uri.EscapeDataString(tenantId)}";
    List<string> queryParams = [];
    if (description is not null)
        queryParams.Add($"description={Uri.EscapeDataString(description)}");
    bool includeSnapshots = !noSnapshots;
    queryParams.Add($"includeSnapshots={includeSnapshots.ToString().ToLowerInvariant()}");
    if (queryParams.Count > 0)
        path += "?" + string.Join("&", queryParams);
    ```
    - Calls `PostAsync<AdminOperationResult>(path, cancellationToken)`.
    - Same `ResultColumns` as snapshot create.
    - On success: print "Backup triggered. Operation ID: {id}" to stderr.
    - On `AdminApiException` with status 403: "Access denied. Admin role required." to stderr.
    - Testable overload:
    ```csharp
    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenantId,
        string? description,
        bool noSnapshots,
        CancellationToken cancellationToken)
    ```

- [x] **Task 6: Backup validate, restore, export-stream, and import-stream sub-subcommands** (AC: 8, 9, 10, 11)
  - [x] 6.1 Create `Commands/Backup/BackupValidateCommand.cs`:
    - One positional argument: `BackupArguments.BackupId()`.
    - Handler: calls `PostAsync<AdminOperationResult>("api/v1/admin/backups/{Uri.EscapeDataString(backupId)}/validate", cancellationToken)`.
    - Same `ResultColumns`.
    - On success: print "Backup validation started. Operation ID: {id}" to stderr.
    - On `AdminApiException` with status 404: "Backup '{backupId}' not found." to stderr.
    - Testable overload:
    ```csharp
    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string backupId,
        CancellationToken cancellationToken)
    ```
  - [x] 6.2 Create `Commands/Backup/BackupRestoreCommand.cs`:
    - One positional argument: `BackupArguments.BackupId()`.
    - Options: `--point-in-time` (`Option<DateTimeOffset?>`, optional ISO 8601), `--dry-run` (`Option<bool>`, default false).
    - Handler: builds query string with optional parameters:
    ```csharp
    string path = $"api/v1/admin/backups/{Uri.EscapeDataString(backupId)}/restore";
    List<string> queryParams = [];
    if (pointInTime is not null)
        queryParams.Add($"pointInTime={Uri.EscapeDataString(pointInTime.Value.ToString("O"))}");
    queryParams.Add($"dryRun={dryRun.ToString().ToLowerInvariant()}");
    if (queryParams.Count > 0)
        path += "?" + string.Join("&", queryParams);
    ```
    - Calls `PostAsync<AdminOperationResult>(path, cancellationToken)`.
    - Same `ResultColumns`.
    - On success when `dryRun` false: print "Restore initiated. Operation ID: {id}" to stderr.
    - On success when `dryRun` true: print "Dry-run restore validated. Operation ID: {id}" to stderr.
    - On `AdminApiException` with status 404: "Backup '{backupId}' not found." to stderr.
    - Testable overload:
    ```csharp
    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string backupId,
        DateTimeOffset? pointInTime,
        bool dryRun,
        CancellationToken cancellationToken)
    ```
  - [x] 6.3 Create `Commands/Backup/BackupExportStreamCommand.cs`:
    - Three positional arguments: `BackupArguments.TenantId()`, `BackupArguments.Domain()`, `BackupArguments.AggregateId()`.
    - Option: `--export-format` (`Option<string>`, default "JSON", accepts "JSON" or "CloudEvents") — named `--export-format` to avoid conflict with global `--format`.
    - Handler: calls `PostAsync<StreamExportResult>("api/v1/admin/backups/export-stream", new StreamExportRequest(tenantId, domain, aggregateId, exportFormat), cancellationToken)`. Uses the existing `StreamExportRequest` record from `Hexalith.EventStore.Admin.Abstractions.Models.Storage` (CLI already references Admin.Abstractions for other DTOs). This provides compile-time type safety over anonymous objects.
    - Column definitions:
    ```csharp
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Success", "Success"),
        new("Tenant", "TenantId"),
        new("Domain", "Domain"),
        new("Aggregate", "AggregateId"),
        new("Events", "EventCount", Align: Alignment.Right),
        new("File Name", "FileName"),
    ];
    ```
    - If `result.Success` is false: print `result.ErrorMessage` to stderr, exit code `2`.
    - On success for table/CSV: display summary columns. For JSON: full `StreamExportResult` serialized.
    - Testable overload:
    ```csharp
    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenantId,
        string domain,
        string aggregateId,
        string exportFormat,
        CancellationToken cancellationToken)
    ```
  - [x] 6.4 Create `Commands/Backup/BackupImportStreamCommand.cs`:
    - One positional argument: `BackupArguments.TenantId()`.
    - Option: `--file` / `-f` (`Option<string>`) — path to the JSON file to import. Mark as required: `fileOption.IsRequired = true;` (`System.CommandLine` does not infer required from type alone for options).
    - Handler:
    ```csharp
    if (!File.Exists(filePath))
    {
        await Console.Error.WriteLineAsync($"File not found: {filePath}").ConfigureAwait(false);
        return ExitCodes.Error;
    }
    string content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
    string path = $"api/v1/admin/backups/import-stream?tenantId={Uri.EscapeDataString(tenantId)}";
    AdminOperationResult result = await client
        .PostAsync<AdminOperationResult>(path, content, cancellationToken)
        .ConfigureAwait(false);
    ```
    - Note: `PostAsync` serializes the string `content` as a JSON string value, which is what `[FromBody] string content` expects. This works because `JsonSerializer.Serialize(stringValue)` produces a JSON string that ASP.NET Core deserializes correctly. **Wire size caveat:** JSON string escaping adds ~20% overhead (e.g., a 10MB file becomes ~12MB on the wire). The server enforces a 10MB `[RequestSizeLimit]` on the endpoint, so practical import ceiling is ~8MB of raw content. For larger imports, stdin pipe support is a future enhancement.
    - Same `ResultColumns`.
    - On success: print "Stream imported. Operation ID: {id}" to stderr.
    - Testable overload:
    ```csharp
    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenantId,
        string filePath,
        CancellationToken cancellationToken)
    ```

- [x] **Task 7: Unit tests** (AC: 16)

  Test file locations:
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Client/AdminApiClientPutDeleteTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Snapshot/SnapshotPoliciesCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Snapshot/SnapshotCreateCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Snapshot/SnapshotSetPolicyCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Snapshot/SnapshotDeletePolicyCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Backup/BackupListCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Backup/BackupTriggerCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Backup/BackupValidateCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Backup/BackupRestoreCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Backup/BackupExportImportCommandTests.cs`

  **Test pattern:** Reuse `MockHttpMessageHandler` from the test project. For import-stream tests that require file I/O, use a temp file via `Path.GetTempFileName()` with cleanup in `Dispose`. Construct `AdminApiClient` with `internal AdminApiClient(GlobalOptions, HttpMessageHandler)` constructor.

  - [x] **AdminApiClient PUT/DELETE tests** (`AdminApiClientPutDeleteTests.cs`):
  - [x] 7.1 `PutAsync_Success_ReturnsDeserializedResponse` — 200 response deserializes correctly.
  - [x] 7.2 `PutAsync_403_ThrowsAdminApiException` — HTTP 403 throws with access denied message.
  - [x] 7.3 `PutAsync_ConnectionRefused_ThrowsAdminApiException` — Socket error wraps correctly.
  - [x] 7.4 `DeleteAsync_Success_ReturnsDeserializedResponse` — 200 response deserializes correctly.
  - [x] 7.5 `DeleteAsync_404_ThrowsAdminApiException` — HTTP 404 throws with not found message.
  - [x] 7.6 `DeleteAsync_ConnectionRefused_ThrowsAdminApiException` — Socket error wraps correctly.

  - [x] **Snapshot policies tests** (`SnapshotPoliciesCommandTests.cs`):
  - [x] 7.7 `SnapshotPoliciesCommand_ReturnsTable` — Table output contains expected headers and data.
  - [x] 7.8 `SnapshotPoliciesCommand_EmptyResult_PrintsNoPoliciesFound` — Empty list, exit code `0`.
  - [x] 7.9 `SnapshotPoliciesCommand_JsonFormat_ReturnsValidJson` — JSON deserializes back to `List<SnapshotPolicy>`.
  - [x] 7.10 `SnapshotPoliciesCommand_TenantFilter_PassesQueryParam` — Verify `?tenantId=xxx` in request URI.

  - [x] **Snapshot create tests** (`SnapshotCreateCommandTests.cs`):
  - [x] 7.11 `SnapshotCreateCommand_Success_ReturnsOperationResult` — Successful POST, exit code `0`.
  - [x] 7.12 `SnapshotCreateCommand_403_ReturnsError` — HTTP 403, exit code `2`.
  - [x] 7.13 `SnapshotCreateCommand_404_ReturnsError` — HTTP 404, exit code `2`.
  - [x] 7.14 `SnapshotCreateCommand_SpecialCharsInIds_AreUrlEncoded` — URL-encodes path parameters.

  - [x] **Snapshot set-policy tests** (`SnapshotSetPolicyCommandTests.cs`):
  - [x] 7.15 `SnapshotSetPolicyCommand_Success_ReturnsOperationResult` — Successful PUT, exit code `0`.
  - [x] 7.16 `SnapshotSetPolicyCommand_PassesIntervalInQueryString` — Verify `?intervalEvents=N` in request URI.
  - [x] 7.17 `SnapshotSetPolicyCommand_403_ReturnsError` — HTTP 403, exit code `2`.

  - [x] **Snapshot delete-policy tests** (`SnapshotDeletePolicyCommandTests.cs`):
  - [x] 7.18 `SnapshotDeletePolicyCommand_Success_ReturnsOperationResult` — Successful DELETE, exit code `0`.
  - [x] 7.19 `SnapshotDeletePolicyCommand_404_ReturnsError` — HTTP 404 "Snapshot policy not found.", exit code `2`.

  - [x] **Backup list tests** (`BackupListCommandTests.cs`):
  - [x] 7.20 `BackupListCommand_ReturnsTable` — Table output with expected headers and data.
  - [x] 7.21 `BackupListCommand_EmptyResult_PrintsNoJobsFound` — Empty list, exit code `0`.
  - [x] 7.22 `BackupListCommand_JsonFormat_ReturnsValidJson` — JSON deserializes back to `List<BackupJob>`.
  - [x] 7.23 `BackupListCommand_EnumsSerializeAsStrings` — `BackupJobStatus.Completed` serializes as `"completed"`, not `2`.
  - [x] 7.24 `BackupListCommand_TenantFilter_PassesQueryParam` — Verify `?tenantId=xxx` in request URI.

  - [x] **Backup trigger tests** (`BackupTriggerCommandTests.cs`):
  - [x] 7.25 `BackupTriggerCommand_Success_ReturnsOperationResult` — Successful POST (202), exit code `0`.
  - [x] 7.26 `BackupTriggerCommand_NoSnapshots_PassesFalse` — `--no-snapshots` passes `includeSnapshots=false` in query string.
  - [x] 7.27 `BackupTriggerCommand_Description_PassesQueryParam` — Verify `description=xxx` in query string.
  - [x] 7.28 `BackupTriggerCommand_403_ReturnsError` — HTTP 403 "Admin role required.", exit code `2`.

  - [x] **Backup validate tests** (`BackupValidateCommandTests.cs`):
  - [x] 7.29 `BackupValidateCommand_Success_ReturnsOperationResult` — Successful POST, exit code `0`.
  - [x] 7.30 `BackupValidateCommand_404_ReturnsError` — HTTP 404 "Backup not found.", exit code `2`.

  - [x] **Backup restore tests** (`BackupRestoreCommandTests.cs`):
  - [x] 7.31 `BackupRestoreCommand_Success_ReturnsOperationResult` — Successful POST, exit code `0`.
  - [x] 7.32 `BackupRestoreCommand_DryRun_PassesQueryParam` — Verify `dryRun=true` in query string.
  - [x] 7.33 `BackupRestoreCommand_PointInTime_PassesQueryParam` — Verify `pointInTime=...` in query string.
  - [x] 7.34 `BackupRestoreCommand_404_ReturnsError` — HTTP 404, exit code `2`.

  - [x] **Backup export/import tests** (`BackupExportImportCommandTests.cs`):
  - [x] 7.35 `BackupExportStreamCommand_Success_ReturnsSummary` — Successful POST, table shows summary, exit code `0`.
  - [x] 7.36 `BackupExportStreamCommand_Failed_ReturnsError` — `result.Success` is false, exit code `2`.
  - [x] 7.37 `BackupExportStreamCommand_ExportFormat_PassesInBody` — Verify `format` field in POST body.
  - [x] 7.38 `BackupImportStreamCommand_Success_ReturnsOperationResult` — Successful import, exit code `0`.
  - [x] 7.39 `BackupImportStreamCommand_FileNotFound_ReturnsError` — Missing file, exit code `2`.
  - [x] 7.40 `BackupImportStreamCommand_PassesTenantIdInQueryString` — Verify `?tenantId=xxx` in request URI.

## Dev Notes

### Builds on story 17-1 infrastructure — do NOT recreate

All of the following already exist from previous stories. Reuse them:
- `GlobalOptions` / `GlobalOptionsBinding` — global option parsing and resolution
- `AdminApiClient` — HTTP client with auth, error handling, shared `JsonSerializerOptions`. Includes `GetAsync<T>`, `TryGetAsync<T>`, `PostAsync<TResponse>`. **This story adds `PutAsync<TResponse>` and `DeleteAsync<TResponse>`.**
- `IOutputFormatter` / `OutputFormatterFactory` / `OutputWriter` — formatting infrastructure
- `ColumnDefinition` / `Alignment` — column metadata for table/CSV
- `ExitCodes` — exit code constants (Success=0, Degraded=1, Error=2)
- `JsonDefaults.Options` — shared `JsonSerializerOptions` with `JsonStringEnumConverter` (camelCase naming)
- `MockHttpMessageHandler` — test helper for HTTP mocking (single response)
- `QueuedMockHttpMessageHandler` — test helper for sequential responses (from story 17-4)

### Admin API endpoints used — snapshot (storage controller)

All endpoints already exist in `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStorageController.cs`:

| Method | Route | Auth | Returns | HTTP Status |
|--------|-------|------|---------|-------------|
| GET | `api/v1/admin/storage/snapshot-policies?tenantId=` | ReadOnly | `IReadOnlyList<SnapshotPolicy>` | 200 |
| POST | `api/v1/admin/storage/{tenantId}/{domain}/{aggregateId}/snapshot` | Operator | `AdminOperationResult` | 200 |
| PUT | `api/v1/admin/storage/{tenantId}/{domain}/{aggregateType}/snapshot-policy?intervalEvents=` | Operator | `AdminOperationResult` | 200 |
| DELETE | `api/v1/admin/storage/{tenantId}/{domain}/{aggregateType}/snapshot-policy` | Operator | `AdminOperationResult` | 200 / 404 |

### Admin API endpoints used — backup (backup controller)

All endpoints already exist in `src/Hexalith.EventStore.Admin.Server/Controllers/AdminBackupsController.cs`:

| Method | Route | Auth | Returns | HTTP Status |
|--------|-------|------|---------|-------------|
| GET | `api/v1/admin/backups?tenantId=` | ReadOnly | `IReadOnlyList<BackupJob>` | 200 |
| POST | `api/v1/admin/backups/{tenantId}?description=&includeSnapshots=` | Admin | `AdminOperationResult` | 202 |
| POST | `api/v1/admin/backups/{backupId}/validate` | Admin | `AdminOperationResult` | 202 |
| POST | `api/v1/admin/backups/{backupId}/restore?pointInTime=&dryRun=` | Admin | `AdminOperationResult` | 202 |
| POST | `api/v1/admin/backups/export-stream` | Admin | `StreamExportResult` | 200 |
| POST | `api/v1/admin/backups/import-stream?tenantId=` | Admin | `AdminOperationResult` | 202 |

### Existing Admin.Abstractions models — do NOT recreate

All DTOs are defined in `src/Hexalith.EventStore.Admin.Abstractions/Models/`:

**Storage models** (`Models/Storage/`):
- `SnapshotPolicy(TenantId, Domain, AggregateType, IntervalEvents, CreatedAtUtc)` — policy config
- `BackupJob(BackupId, TenantId, StreamId?, Description?, JobType, Status, IncludeSnapshots, CreatedAtUtc, CompletedAtUtc?, EventCount?, SizeBytes?, IsValidated, ErrorMessage?)` — backup job
- `BackupJobStatus` enum: `Pending`, `Running`, `Completed`, `Failed`, `Validating`
- `BackupJobType` enum: `Backup`, `Restore`
- `StreamExportRequest(TenantId, Domain, AggregateId, Format)` — export request
- `StreamExportResult(Success, TenantId, Domain, AggregateId, EventCount, Content?, FileName?, ErrorMessage?)` — export result

**Common models** (`Models/Common/`):
- `AdminOperationResult(Success, OperationId, Message?, ErrorCode?)` — write operation result

### AdminApiClient extension — PUT and DELETE methods

The existing `AdminApiClient` only has GET and POST methods. This story adds:
- `PutAsync<TResponse>(string path, CancellationToken cancellationToken)` — for `snapshot set-policy`
- `DeleteAsync<TResponse>(string path, CancellationToken cancellationToken)` — for `snapshot delete-policy`

Both follow the identical error handling pattern as `PostAsync`. The PUT method uses `_httpClient.PutAsync(path, null, ct)` since the snapshot policy endpoint takes `intervalEvents` as a query parameter, not a body.

### Shared ResultColumns for write operations

All write sub-subcommands (snapshot create, set-policy, delete-policy, backup trigger, validate, restore, import-stream) return `AdminOperationResult`. Each command file defines its own `ResultColumns` (identical content, but per-file to keep commands self-contained):
```csharp
internal static readonly List<ColumnDefinition> ResultColumns =
[
    new("Operation ID", "OperationId"),
    new("Success", "Success"),
    new("Message", "Message"),
];
```

For CSV format, single-object results (AdminOperationResult) render as one header row + one data row, matching the table formatter's `Format()` (single-object) behavior. This is consistent with how `HealthCommand` and `ProjectionStatusCommand` handle single-object CSV output.

### Nullable fields in BackupJob table rendering

`BackupJob` has nullable fields: `EventCount?`, `SizeBytes?`, `CompletedAtUtc?`. When a backup job is still running (`Status = Running`), these will be null. The table formatter renders null values as empty cells. This is acceptable — no special handling needed. JSON format serializes nulls as `null`. CSV renders empty fields between commas.

### AdminApiException.StatusCode property

The `AdminApiException` class (in `Client/AdminApiException.cs`) exposes a `StatusCode` property (`int`). The `PostAsync`, `PutAsync`, and `DeleteAsync` methods pass the HTTP status code when constructing the exception (e.g., `new AdminApiException(message, 403)`). The error handling pattern below uses `ex.StatusCode` for status-specific catch filters. The `GetAsync` method does NOT pass status codes — it only throws without a status code for 404 (which it handles via `TryGetAsync` returning null instead).

### Error handling pattern for write operations

Write operations throw `AdminApiException` on HTTP errors. Each command catches and maps to user-friendly messages:
```csharp
try
{
    AdminOperationResult result = await client
        .PostAsync<AdminOperationResult>(path, cancellationToken)
        .ConfigureAwait(false);
    // format and output result...
    await Console.Error.WriteLineAsync($"Operation completed. Operation ID: {result.OperationId}")
        .ConfigureAwait(false);
    return ExitCodes.Success;
}
catch (AdminApiException ex) when (ex.StatusCode == 403)
{
    await Console.Error.WriteLineAsync("Access denied. Operator role required.").ConfigureAwait(false);
    return ExitCodes.Error;
}
catch (AdminApiException ex) when (ex.StatusCode == 404)
{
    await Console.Error.WriteLineAsync("Resource not found.").ConfigureAwait(false);
    return ExitCodes.Error;
}
catch (AdminApiException ex)
{
    await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
    return ExitCodes.Error;
}
```

### import-stream — file reading and JSON body serialization

The import-stream endpoint expects `[FromBody] string content` with content type `application/json`. The existing `PostAsync` uses `JsonSerializer.Serialize(body, JsonDefaults.Options)` which, when `body` is a `string`, wraps it in JSON quotes. ASP.NET Core's `[FromBody] string` correctly deserializes JSON string values, so this works transparently. Example:
- File content: `[{"type": "foo"}, ...]`
- `PostAsync` sends: `"[{\"type\": \"foo\"}, ...]"` (a valid JSON string)
- Server receives: `[{"type": "foo"}, ...]` (the original content)

### URL encoding — use Uri.EscapeDataString for all path parameters

All user-provided path parameters (tenantId, domain, aggregateId, aggregateType, backupId) are URL-encoded:
```csharp
string path = $"api/v1/admin/storage/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateId)}/snapshot";
```

### --tenant filter pattern (from story 17-3)

The `--tenant` / `-T` filter for `snapshot policies` and `backup list` follows the `ProjectionListCommand` pattern:
```csharp
Option<string?> tenantOption = new("--tenant", "Filter by tenant ID");
tenantOption.AddAlias("-T");
command.Options.Add(tenantOption);
```

### --no-snapshots flag inversion for backup trigger

The API defaults to `includeSnapshots=true`. The CLI uses `--no-snapshots` (default false) for cleaner UX:
```csharp
Option<bool> noSnapshotsOption = new("--no-snapshots", "Exclude snapshots from backup");
// ...
bool includeSnapshots = !noSnapshots;
queryParams.Add($"includeSnapshots={includeSnapshots.ToString().ToLowerInvariant()}");
```

### --export-format vs --format

The global `--format` option controls output rendering (table/json/csv). The `--export-format` option on `backup export-stream` controls the server-side export format (JSON/CloudEvents). These are distinct:
- `--format json` = render the StreamExportResult as JSON output
- `--export-format CloudEvents` = tell the server to export events in CloudEvents format

### Authorization levels — snapshot vs backup

- **Snapshot** operations require **Operator** role (ReadOnly for listing policies)
- **Backup** write operations require **Admin** role (ReadOnly for listing jobs)
- CLI error messages distinguish: "Operator role required" vs "Admin role required"

### Code style — follow project conventions exactly

- File-scoped namespaces: `namespace Hexalith.EventStore.Admin.Cli.Commands.Snapshot;`
- Allman braces (opening brace on new line)
- Private fields: `_camelCase`
- Async methods: `Async` suffix
- 4 spaces indentation, CRLF line endings, UTF-8
- Nullable enabled
- Implicit usings enabled
- Warnings as errors

### Handler wiring pattern (from story 17-1)

```csharp
command.SetAction(async (parseResult, cancellationToken) =>
{
    GlobalOptions options = binding.Resolve(parseResult);
    string tenantId = parseResult.GetValue(tenantIdArg)!;
    return await ExecuteAsync(options, tenantId, cancellationToken)
        .ConfigureAwait(false);
});
```

### JSON output uses camelCase — critical for scriptability

JSON output uses `JsonNamingPolicy.CamelCase` via `JsonDefaults.Options`. Property names: `backupId`, `tenantId`, `jobType`, `status`, `includeSnapshots`, `createdAtUtc`, `completedAtUtc`, `eventCount`, `sizeBytes`, `isValidated`, `errorMessage`, `intervalEvents`, `aggregateType`, `operationId`, `success`, `message`, `errorCode`. Operators write `jq` filters like `jq '.[] | select(.status == "completed")'`.

### CI/CD usage examples

```bash
# List snapshot policies
eventstore-admin snapshot policies --format json

# List policies for a specific tenant
eventstore-admin snapshot policies --tenant acme-corp

# Create on-demand snapshot
eventstore-admin snapshot create acme-corp counter order-123

# Set auto-snapshot policy (every 50 events)
eventstore-admin snapshot set-policy acme-corp counter counter 50

# Delete auto-snapshot policy
eventstore-admin snapshot delete-policy acme-corp counter counter

# List all backup jobs
eventstore-admin backup list --format json

# Trigger backup for a tenant
eventstore-admin backup trigger acme-corp --description "Weekly backup"

# Trigger backup without snapshots
eventstore-admin backup trigger acme-corp --no-snapshots

# Validate a backup
eventstore-admin backup validate bkp-abc123

# Restore from backup (dry run first)
eventstore-admin backup restore bkp-abc123 --dry-run
eventstore-admin backup restore bkp-abc123

# Point-in-time restore
eventstore-admin backup restore bkp-abc123 --point-in-time 2026-03-20T10:00:00Z

# Export a stream
eventstore-admin backup export-stream acme-corp counter order-123 --output stream-export.json

# Import a stream
eventstore-admin backup import-stream acme-corp --file stream-export.json

# CI/CD: Check backup status
eventstore-admin backup list --tenant acme-corp --format json | jq '.[] | select(.status == "failed")'
```

### Folder structure

```
src/Hexalith.EventStore.Admin.Cli/
  Client/
    AdminApiClient.cs                  <-- MODIFIED (add PutAsync, DeleteAsync)
  Commands/
    Snapshot/
      SnapshotCommand.cs               <-- NEW (parent command)
      SnapshotArguments.cs             <-- NEW (shared positional arguments)
      SnapshotPoliciesCommand.cs       <-- NEW
      SnapshotCreateCommand.cs         <-- NEW
      SnapshotSetPolicyCommand.cs      <-- NEW
      SnapshotDeletePolicyCommand.cs   <-- NEW
    Backup/
      BackupCommand.cs                 <-- NEW (parent command)
      BackupArguments.cs               <-- NEW (shared positional arguments)
      BackupListCommand.cs             <-- NEW
      BackupTriggerCommand.cs          <-- NEW
      BackupValidateCommand.cs         <-- NEW
      BackupRestoreCommand.cs          <-- NEW
      BackupExportStreamCommand.cs     <-- NEW
      BackupImportStreamCommand.cs     <-- NEW
  Program.cs                           <-- MODIFIED (replace two stubs)

tests/Hexalith.EventStore.Admin.Cli.Tests/
  Client/
    AdminApiClientPutDeleteTests.cs    <-- NEW
  Commands/
    Snapshot/
      SnapshotPoliciesCommandTests.cs  <-- NEW
      SnapshotCreateCommandTests.cs    <-- NEW
      SnapshotSetPolicyCommandTests.cs <-- NEW
      SnapshotDeletePolicyCommandTests.cs <-- NEW
    Backup/
      BackupListCommandTests.cs        <-- NEW
      BackupTriggerCommandTests.cs     <-- NEW
      BackupValidateCommandTests.cs    <-- NEW
      BackupRestoreCommandTests.cs     <-- NEW
      BackupExportImportCommandTests.cs <-- NEW
```

### Modified files

- `src/Hexalith.EventStore.Admin.Cli/Client/AdminApiClient.cs` — add `PutAsync<TResponse>` and `DeleteAsync<TResponse>` methods
- `src/Hexalith.EventStore.Admin.Cli/Program.cs` — replace snapshot and backup stubs with `SnapshotCommand.Create(binding)` and `BackupCommand.Create(binding)`
- No changes to `ExitCodes`, formatting infrastructure, `GlobalOptions`, `GlobalOptionsBinding`, or any other existing files

### Git commit patterns from recent work

Recent commits: `feat: <description> for story <story-id>`
Branch naming: `feat/story-17-6-snapshot-and-backup-subcommands`

### Previous story intelligence (17-1 through 17-5)

Story 17-1 established:
- `System.CommandLine` package version in `Directory.Packages.props`
- Root command structure in `Program.cs` with `GlobalOptionsBinding` pattern
- `AdminApiClient` with `GetAsync<T>`, `TryGetAsync<T>`, and `PostAsync<TResponse>` methods
- Output formatting infrastructure
- `ExitCodes` static class (Success=0, Degraded=1, Error=2)
- `MockHttpMessageHandler` test helper
- Handler wiring: `command.SetAction(async (parseResult, cancellationToken) => { ... })`

Story 17-2 established:
- Stream subcommand folder organization pattern (`Commands/Stream/`)
- Shared positional arguments pattern (`StreamArguments.cs`)
- URL encoding of path parameters using `Uri.EscapeDataString`

Story 17-3 established:
- Parent command with sub-subcommands pattern (`ProjectionCommand`)
- `PostAsync<TResponse>` for write operations
- Dual-section table output pattern (overview + details)
- Two `ExecuteAsync` overloads (public + internal for testing)
- `--tenant` filter option pattern (alias `-T` since `-t` is taken by `--token`)

Story 17-4 established:
- `--quiet` option suppressing stdout output
- `QueuedMockHttpMessageHandler` for tests requiring sequential responses
- CI/CD exit code patterns (0/1/2)
- `HealthDaprCommand` — sub-subcommand of a subcommand

Story 17-5 established:
- Variadic positional arguments (`Argument<string[]>`)
- `TryGetAsync<T>` for 404-safe GET requests
- Composite result records (`TenantVerifyResult`)
- Verify/validation patterns with PASS/WARNING/FAIL verdicts
- Six sub-subcommands on a single parent

Follow these as canonical patterns.

### Architecture: CLI is a thin HTTP client (ADR-P4)

Per ADR-P4, the CLI never accesses DAPR directly. All snapshot and backup operations go through `AdminApiClient` -> Admin REST API -> DAPR. No DAPR SDK, no Aspire dependencies.

### Future enhancements (out of scope for this story)

- **`--quiet` mode on `backup trigger` / `backup validate` / `backup restore`** — exit code only for CI/CD scripts. Follows the `--quiet` pattern from `HealthCommand` (story 17-4) and `TenantVerifyCommand` (story 17-5). Useful for `backup trigger acme-corp --quiet` in automated pipelines.
- **`--watch` mode on `backup list`** — continuous monitoring with polling interval.
- **`snapshot hot-streams`** — display streams with highest snapshot ages (calls `GetHotStreamsAsync`).
- **`backup schedule`** — configure automated backup schedules.
- **Stdin pipe for import-stream** — `cat events.json | eventstore-admin backup import-stream acme-corp` — bypasses JSON string encoding overhead and the 8MB practical ceiling.
- **Progress reporting for long-running operations** — poll operation status with `--wait`.

### References

- [Source: _bmad-output/planning-artifacts/prd.md — FR13: configurable snapshots]
- [Source: _bmad-output/planning-artifacts/prd.md — FR76: storage management, snapshot/backup operations]
- [Source: _bmad-output/planning-artifacts/prd.md — FR79: three-interface shared Admin API]
- [Source: _bmad-output/planning-artifacts/prd.md — FR80: CLI output formats, exit codes]
- [Source: _bmad-output/planning-artifacts/prd.md — NFR42: CLI startup + query within 3 seconds]
- [Source: _bmad-output/planning-artifacts/architecture.md — ADR-P4: CLI is thin HTTP client, no DAPR]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR50: snapshot and backup subcommands in CLI tree]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR52: exit codes 0/1/2 for CI/CD]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-03-21-admin-tooling.md — admin CLI story 17.6]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Storage/ — all snapshot and backup DTOs]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Common/AdminOperationResult.cs — write operation result]
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/AdminStorageController.cs — snapshot REST API endpoints]
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/AdminBackupsController.cs — backup REST API endpoints]
- [Source: src/Hexalith.EventStore.Admin.Cli/Client/AdminApiClient.cs — HTTP client (to be extended)]
- [Source: src/Hexalith.EventStore.Admin.Cli/Commands/Projection/ — projection subcommand (pattern reference)]
- [Source: src/Hexalith.EventStore.Admin.Cli/Commands/Stream/ — stream subcommand (pattern reference)]
- [Source: src/Hexalith.EventStore.Admin.Cli/ExitCodes.cs — exit code constants]
- [Source: _bmad-output/implementation-artifacts/17-5-tenant-subcommand-list-quotas-verify.md — tenant story (pattern reference)]
- [Source: _bmad-output/implementation-artifacts/16-2-snapshot-management-and-auto-snapshot-policies.md — Web UI snapshot story (API reference)]
- [Source: _bmad-output/implementation-artifacts/16-4-backup-and-restore-console.md — Web UI backup story (API reference)]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Build error CS1061: `SetDefaultValue` and `IsRequired` not available on System.CommandLine Option. Fixed by using `DefaultValueFactory` and `Required` property in constructor initializer instead.
- Pre-existing build error CS0433 in IntegrationTests (PerConsumerRateLimitingTests.cs) — ambiguous `Program` type. Not related to this story.

### Completion Notes List

- Task 1: Added `PutAsync<TResponse>` and `DeleteAsync<TResponse>` to `AdminApiClient`, following the exact same error handling pattern as `PostAsync`. PUT uses `_httpClient.PutAsync(path, null, ct)` since snapshot policy endpoint takes parameters via query string.
- Task 2: Created `SnapshotCommand` parent command and `SnapshotArguments` shared positional arguments. Updated `Program.cs` to replace snapshot stub.
- Task 3: Implemented 4 snapshot sub-subcommands: `policies` (list with `--tenant`/`-T` filter), `create` (POST with 3 positional args), `set-policy` (PUT with 4 positional args including `intervalEvents`), `delete-policy` (DELETE with 3 positional args). All use URL encoding via `Uri.EscapeDataString` and status-specific error messages.
- Task 4: Created `BackupCommand` parent command and `BackupArguments` shared positional arguments. Updated `Program.cs` to replace backup stub.
- Task 5: Implemented `backup list` (GET with `--tenant`/`-T` filter, 8 columns including nullable fields) and `backup trigger` (POST with `--description`/`-d` and `--no-snapshots` flag inversion).
- Task 6: Implemented `backup validate` (POST with backupId positional arg), `backup restore` (POST with `--point-in-time` and `--dry-run`), `backup export-stream` (POST with body using `StreamExportRequest`, `--export-format` option), `backup import-stream` (file reading with `--file`/`-f` required option, POST with file content as body).
- Task 7: Created 10 test files with 40 test methods covering: AdminApiClient PUT/DELETE (6 tests), snapshot policies (4 tests), snapshot create (4 tests), snapshot set-policy (3 tests), snapshot delete-policy (2 tests), backup list (5 tests), backup trigger (4 tests), backup validate (2 tests), backup restore (4 tests), backup export/import (6 tests). All 191 tests pass (including 151 pre-existing).

### Implementation Plan

Followed story file task sequence exactly. Each command follows the established patterns from stories 17-1 through 17-5: static `Create()` factory, dual `ExecuteAsync()` overloads (public creates client, internal takes client for testing), `IOutputFormatter`/`OutputWriter` for output, `AdminApiException` catch with `HttpStatusCode` switch for status-specific messages.

### File List

**New files (14 source + 10 test = 24 total):**
- `src/Hexalith.EventStore.Admin.Cli/Commands/Snapshot/SnapshotCommand.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Snapshot/SnapshotArguments.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Snapshot/SnapshotPoliciesCommand.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Snapshot/SnapshotCreateCommand.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Snapshot/SnapshotSetPolicyCommand.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Snapshot/SnapshotDeletePolicyCommand.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Backup/BackupCommand.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Backup/BackupArguments.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Backup/BackupListCommand.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Backup/BackupTriggerCommand.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Backup/BackupValidateCommand.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Backup/BackupRestoreCommand.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Backup/BackupExportStreamCommand.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Backup/BackupImportStreamCommand.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Client/AdminApiClientPutDeleteTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Snapshot/SnapshotPoliciesCommandTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Snapshot/SnapshotCreateCommandTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Snapshot/SnapshotSetPolicyCommandTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Snapshot/SnapshotDeletePolicyCommandTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Backup/BackupListCommandTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Backup/BackupTriggerCommandTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Backup/BackupValidateCommandTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Backup/BackupRestoreCommandTests.cs`
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Backup/BackupExportImportCommandTests.cs`

**Modified files (2):**
- `src/Hexalith.EventStore.Admin.Cli/Client/AdminApiClient.cs` — added `PutAsync<TResponse>` and `DeleteAsync<TResponse>` methods
- `src/Hexalith.EventStore.Admin.Cli/Program.cs` — replaced snapshot and backup stubs with `SnapshotCommand.Create(binding)` and `BackupCommand.Create(binding)`

### Change Log

- 2026-03-26: Implemented story 17-6 — snapshot and backup subcommands for eventstore-admin CLI. Added 10 sub-subcommands (4 snapshot + 6 backup) with full output formatting, exit codes, error handling, and 40 unit tests. Extended AdminApiClient with PUT and DELETE HTTP methods.
