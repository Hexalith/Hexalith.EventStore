# Story 17.3: Projection Subcommand — List, Status, Pause, Resume, Reset

Status: done

Size: Medium — ~8 new files, 5 task groups, 12 ACs, ~28 tests (~8-10 hours estimated). Replaces the `projection` stub from story 17-1 with five sub-subcommands (`list`, `status`, `pause`, `resume`, `reset`) that call the existing Admin API projection endpoints. Reuses output formatting, `AdminApiClient`, `GlobalOptionsBinding`, and exit code infrastructure from 17-1. First CLI story to include write operations (POST), requiring a new `PostAsync<TResponse>` method on `AdminApiClient`.

**Dependency:** Story 17-1 must be complete (done). This story builds on the CLI scaffold, global options, output formatting, `AdminApiClient`, and exit code conventions established there. Story 17-2 patterns are followed but 17-2 completion is not a prerequisite.

## Definition of Done

- All 12 ACs verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- All five `projection` sub-subcommands return formatted output with correct exit codes
- `eventstore-admin projection list` returns a table of projection statuses
- `eventstore-admin projection pause <tenant> <name>` sends POST and displays operation result
- Write operations (pause/resume/reset) print confirmation to stderr

## Story

As a **platform operator or CI/CD pipeline engineer**,
I want **`eventstore-admin projection` sub-subcommands to list projections, inspect projection status with error details, and pause, resume, or reset projections**,
so that **I can monitor projection health, troubleshoot projection errors, and control projection lifecycle from the terminal or CI/CD pipelines without needing the Web UI (FR79, FR80, NFR42)**.

## Acceptance Criteria

1. **`projection list` subcommand** — `eventstore-admin projection list [--tenant <id>]` calls `GET /api/v1/admin/projections?tenantId={t}` and displays a table of `ProjectionStatus` records. Columns: Name, Tenant (TenantId), Status (ProjectionStatusType), Lag, Throughput, Errors (ErrorCount), Last Position (LastProcessedPosition), Last Processed (LastProcessedUtc). Exit code `0` on success, `2` on error. Empty result set returns exit code `0` with "No projections found." message to stderr. Note: this endpoint returns a plain JSON array (not `PagedResult`) — deserialize as `List<ProjectionStatus>` (concrete type). An empty list means no projections registered.

2. **`projection status` subcommand** — `eventstore-admin projection status <tenant> <name>` calls `GET /api/v1/admin/projections/{tenant}/{name}` and displays `ProjectionDetail`. For `table` format (dual-section, same pattern as `HealthCommand`):
   - **Section 1 — Overview:** Key/value pairs: Name, Tenant, Status, Lag, Throughput, Error Count (ErrorCount), Last Position (LastProcessedPosition), Last Processed (LastProcessedUtc), Subscribed Events (comma-separated list from SubscribedEventTypes), Configuration. Set `MaxWidth = 80` on Configuration column to prevent layout blowout (full value visible in `--format json`).
   - **Section 2 — Recent Errors:** Table of `ProjectionError` with columns: Position, Timestamp, Event Type (EventTypeName), Message. Set `MaxWidth = 60` on Message column. Only rendered when `Errors` is non-empty.
   For `json` format: full `ProjectionDetail` serialized. For `csv` format: errors table only (if errors exist, else overview key/value as single CSV row). HTTP 404 prints "Projection '{name}' not found in tenant '{tenant}'." to stderr, exit code `2`.

3. **`projection pause` subcommand** — `eventstore-admin projection pause <tenant> <name>` calls `POST /api/v1/admin/projections/{tenant}/{name}/pause` with empty body and displays `AdminOperationResult`. On success: prints "Projection '{name}' paused successfully. Operation ID: {operationId}" to stderr, exit code `0`. On failure (operation result `Success == false`): prints error message from `AdminOperationResult.Message`, exit code `2`. HTTP 404 (from server's `MapOperationResult` with `ErrorCode == "NotFound"`) prints "Projection '{name}' not found in tenant '{tenant}'." to stderr, exit code `2`. HTTP 403 prints "Access denied. Operator role required to pause projections." to stderr, exit code `2`.

4. **`projection resume` subcommand** — `eventstore-admin projection resume <tenant> <name>` calls `POST /api/v1/admin/projections/{tenant}/{name}/resume` with empty body and displays `AdminOperationResult`. Same output pattern as `pause`. On success: "Projection '{name}' resumed successfully. Operation ID: {operationId}". HTTP 403: "Access denied. Operator role required to resume projections.".

5. **`projection reset` subcommand** — `eventstore-admin projection reset <tenant> <name> [--from <position>]` calls `POST /api/v1/admin/projections/{tenant}/{name}/reset` with JSON body `{ "fromPosition": <position> }` (or `{ "fromPosition": null }` when `--from` is omitted). Always sends a JSON body — never send null/empty content for reset. Displays `AdminOperationResult`. On success: "Projection '{name}' reset initiated. Operation ID: {operationId}\nCheck progress: eventstore-admin projection status {tenant} {name}" to stderr, exit code `0`. Note: exit code `0` means the server accepted the reset request, not that the reset is complete — reset returns HTTP 202 Accepted (async operation). The CLI treats 200 and 202 identically for `AdminOperationResult` parsing. `--from` is an optional `Option<long?>` (nullable value type — `parseResult.GetValue(fromOption)` returns `null` when omitted, which maps directly to `new { FromPosition = (long?)null }`). No alias for `--from`. HTTP 403: "Access denied. Operator role required to reset projections.".

6. **Positional arguments** — `tenant` and `name` are `Argument<string>` (positional, required) on all sub-subcommands except `list`. Missing required arguments produce `System.CommandLine`'s built-in error message and usage help.

7. **`PostAsync<TResponse>` on AdminApiClient** — New method for write operations. Sends a POST request with optional JSON body, deserializes the response. Must handle both 200 and 202 status codes as success. Error handling matches `GetAsync<T>` for 401/403/5xx, connection refused, timeout, and JSON deserialization failures. For 404: returns a typed result (not null) since the server returns `ProblemDetails` on 404, not an `AdminOperationResult` — the CLI should detect 404 status and throw `AdminApiException` with the subcommand-specific message. Signature:
   ```csharp
   public async Task<TResponse> PostAsync<TResponse>(string path, object? body, CancellationToken ct)
   ```
   Additionally, add a bodyless overload:
   ```csharp
   public async Task<TResponse> PostAsync<TResponse>(string path, CancellationToken ct)
       => await PostAsync<TResponse>(path, null, ct).ConfigureAwait(false);
   ```

8. **Error handling** — All sub-subcommands reuse `AdminApiClient` error handling from story 17-1 for HTTP 401/403/5xx, connection refused, timeout, and JSON deserialization failures. For write operations (pause/resume/reset): HTTP 403 is expected when the JWT token lacks Operator role — print a subcommand-specific permission message. HTTP 404 from write operations: the server's `MapOperationResult` translates `ErrorCode == "NotFound"` to HTTP 404 — the CLI detects 404 in `PostAsync` and throws `AdminApiException` so command handlers print subcommand-specific messages. All errors go to stderr, exit code `2`.

9. **Test coverage** — Unit tests cover: each sub-subcommand's output formatting (table, JSON, CSV), exit code mapping (success/error), HTTP 404 handling, positional argument parsing, required option validation, operation result display, enum serialization as strings (not integers), URL encoding of special characters in arguments, `PostAsync` integration via `MockHttpMessageHandler`, and 403 permission error messages.

10. **Parent `projection` command help** — Running `eventstore-admin projection` with no subcommand prints help listing all five sub-subcommands (`list`, `status`, `pause`, `resume`, `reset`) with their descriptions. `System.CommandLine` provides this automatically for parent commands with no handler.

11. **Write operation output contract** — Write operations (pause/resume/reset) produce two distinct outputs:
    - **stderr** (human message): Always prints a one-line confirmation or error message. Success: "Projection '{name}' paused successfully. Operation ID: {id}". Failure: error message from `AdminOperationResult.Message` or `AdminApiException.Message`. This line is always printed regardless of `--format`.
    - **stdout** (machine-parseable): The `AdminOperationResult` formatted per `--format` (JSON, table key/value, or single-row CSV). This is the pipeable output for scripts.
    This separation ensures `eventstore-admin projection pause acme counter-view --format json | jq .operationId` works correctly — human messages go to stderr, structured data goes to stdout.

## Tasks / Subtasks

- [x] **Task 1: Add `PostAsync<TResponse>` to `AdminApiClient` and create parent command** (AC: 6, 7, 8)
  - [x] 1.1 Add `PostAsync<TResponse>(string path, object? body, CancellationToken ct)` to `AdminApiClient`:
    - Serializes `body` as JSON using `JsonDefaults.Options`. When `body` is null (bodyless overload for pause/resume), sends `new StringContent("{}", Encoding.UTF8, "application/json")` — an empty JSON object, not null content. This ensures consistent `Content-Type: application/json` header on all POST requests.
    - Sends POST request via `_httpClient.PostAsync`.
    - Handles both HTTP 200 and 202 as success (deserializes response body as `TResponse`).
    - HTTP 401: throws `AdminApiException` with "Authentication required" message.
    - HTTP 403: throws `AdminApiException` with "Access denied. Insufficient permissions." message.
    - HTTP 404: throws `AdminApiException` with "Resource not found" message.
    - HTTP 5xx: throws `AdminApiException` with "Admin API server error" message.
    - Connection errors, timeouts, JSON errors: identical to `GetAsync<T>`.
    - Add bodyless overload `PostAsync<TResponse>(string path, CancellationToken ct)`.
  - [x] 1.2 Add `internal` constructor overload for `PostAsync` testing: same pattern as `GetAsync` — uses `MockHttpMessageHandler` via `internal AdminApiClient(GlobalOptions, HttpMessageHandler)`.
  - [x] 1.3 Create `Commands/Projection/ProjectionCommand.cs` — parent command with no handler:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli.Commands.Projection;
    public static class ProjectionCommand
    {
        public static Command Create(GlobalOptionsBinding binding)
        {
            Command command = new("projection", "List, pause, resume, and reset projections");
            command.Subcommands.Add(ProjectionListCommand.Create(binding));
            command.Subcommands.Add(ProjectionStatusCommand.Create(binding));
            command.Subcommands.Add(ProjectionPauseCommand.Create(binding));
            command.Subcommands.Add(ProjectionResumeCommand.Create(binding));
            command.Subcommands.Add(ProjectionResetCommand.Create(binding));
            return command;
        }
    }
    ```
  - [x] 1.4 Create `Commands/Projection/ProjectionArguments.cs` — shared positional arguments:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli.Commands.Projection;
    public static class ProjectionArguments
    {
        public static Argument<string> Tenant() => new("tenant", "Tenant identifier");
        public static Argument<string> Name() => new("name", "Projection name");
    }
    ```
  - [x] 1.5 Update `Program.cs` — replace `StubCommands.Create("projection", ...)` with `ProjectionCommand.Create(binding)`. Add `using Hexalith.EventStore.Admin.Cli.Commands.Projection;`. Keep all other stubs unchanged.

- [x] **Task 2: `projection list` subcommand** (AC: 1)
  - [x] 2.1 Create `Commands/Projection/ProjectionListCommand.cs`:
    - Options: `--tenant <id>` (alias `-T`, optional). Note: `-t` is taken by global `--token`, so use `-T` for tenant (same convention as `stream list`).
    - Handler: resolves global options via `binding.Resolve(parseResult)`, creates `AdminApiClient`, calls `GET /api/v1/admin/projections?tenantId={t}`.
    - Deserializes as `List<ProjectionStatus>` (concrete type — do NOT use `IReadOnlyList<ProjectionStatus>` as the generic type parameter; `JsonSerializer.Deserialize<T>` requires a concrete type). This endpoint returns a plain JSON array, not `PagedResult`.
    - Formats via `FormatCollection` with column definitions: Name, Tenant (TenantId), Status (Status), Lag (Lag, right-aligned), Throughput (Throughput, right-aligned), Errors (ErrorCount, right-aligned), Last Position (LastProcessedPosition, right-aligned), Last Processed (LastProcessedUtc).
    - When list is empty: print "No projections found." to stderr, exit code `0`.
    - JSON format: serialize the entire `List<ProjectionStatus>`.
    - CSV format: collection only (same as table).

- [x] **Task 3: `projection status` subcommand** (AC: 2)
  - [x] 3.1 Create `Commands/Projection/ProjectionStatusCommand.cs`:
    - Two positional arguments via `ProjectionArguments.Tenant()` and `ProjectionArguments.Name()`.
    - Handler: calls `GET /api/v1/admin/projections/{tenant}/{name}`.
    - Uses `TryGetAsync<ProjectionDetail>` for 404 handling.
    - Table format (dual-section, same pattern as `HealthCommand`):
      - Section 1: construct overview object with: Name, Tenant (TenantId), Status, Lag, Throughput, Error Count (ErrorCount), Last Position (LastProcessedPosition), Last Processed (LastProcessedUtc), Subscribed Events (`string.Join(", ", SubscribedEventTypes)`), Configuration. Render via `formatter.Format(overview, columns)` with `MaxWidth = 80` on Configuration.
      - Section 2 (conditional): if `Errors.Count > 0`, render `Errors` collection with columns: Position, Timestamp, Event Type (EventTypeName), Message (MaxWidth = 60). Render via `formatter.FormatCollection(detail.Errors.ToList(), errorColumns)`.
      - Blank line between sections.
    - JSON format: full `ProjectionDetail` serialized.
    - CSV format: errors table only (if errors exist, else empty).
    - HTTP 404: on null result from `TryGetAsync`, print "Projection '{name}' not found in tenant '{tenant}'." to stderr, exit code `2`.

- [x] **Task 4: `projection pause`, `projection resume`, `projection reset` subcommands** (AC: 3, 4, 5, 11, 12)
  - [x] 4.1 Create `Commands/Projection/ProjectionPauseCommand.cs`:
    - Two positional arguments via `ProjectionArguments`.
    - Handler: calls `POST /api/v1/admin/projections/{tenant}/{name}/pause` via `PostAsync<AdminOperationResult>(path, ct)` (no body).
    - On success (`result.Success == true`): print confirmation to stderr, format result to stdout.
    - On failure: print `result.Message` to stderr, exit code `2`.
    - AdminApiException (401/403/404/5xx): catch and print to stderr, exit code `2`.
  - [x] 4.2 Create `Commands/Projection/ProjectionResumeCommand.cs`:
    - Same pattern as pause. Calls `.../resume`.
  - [x] 4.3 Create `Commands/Projection/ProjectionResetCommand.cs`:
    - Two positional arguments plus optional `--from <position>` option (`Option<long?>`).
    - Handler: calls `POST /api/v1/admin/projections/{tenant}/{name}/reset` with JSON body `{ "fromPosition": <value> }`.
    - Uses `PostAsync<AdminOperationResult>(path, new { FromPosition = fromPosition }, ct)`.
    - Same success/failure output pattern as pause/resume.

- [x] **Task 5: Unit tests** (AC: 9)
  - [x] **Projection list tests:**
  - [x] 5.1 `ProjectionListCommand_ReturnsProjectionTable` — Mocked response with 2 `ProjectionStatus` items renders table with correct columns.
  - [x] 5.2 `ProjectionListCommand_EmptyResult_PrintsNoProjectionsFound` — Empty list prints "No projections found." to stderr, exit code `0`.
  - [x] 5.3 `ProjectionListCommand_JsonFormat_ReturnsValidJson` — JSON output deserializes back to `List<ProjectionStatus>`.
  - [x] 5.4 `ProjectionListCommand_WithTenantFilter_SendsQueryParameter` — `--tenant acme` sends `?tenantId=acme` query string.
  - [x] 5.5 `ProjectionListCommand_CsvFormat_ReturnsHeaderAndRows` — CSV output has header row (Name, TenantId, Status, Lag, Throughput, ErrorCount, ...) and data rows with correct values. Enum values render as strings.
  - [x] **Projection status tests:**
  - [x] 5.6 `ProjectionStatusCommand_ReturnsDualSectionOutput` — Table output contains overview section with all fields including Subscribed Events as comma-separated list AND errors table (when errors exist).
  - [x] 5.7 `ProjectionStatusCommand_NoErrors_OmitsErrorsSection` — Table output omits errors section when `Errors` is empty.
  - [x] 5.8 `ProjectionStatusCommand_NotFound_PrintsError` — HTTP 404 prints "Projection 'xxx' not found in tenant 'yyy'." to stderr, exit code `2`.
  - [x] 5.9 `ProjectionStatusCommand_JsonFormat_ReturnsFullDetail` — JSON includes `errors`, `configuration`, `subscribedEventTypes` fields.
  - [x] **Projection pause tests:**
  - [x] 5.10 `ProjectionPauseCommand_Success_PrintsConfirmation` — Mocked 200 response with `Success=true` prints confirmation to stderr with operation ID.
  - [x] 5.11 `ProjectionPauseCommand_OperationFailure_PrintsError` — Mocked 200 response with `Success=false` prints error message, exit code `2`.
  - [x] 5.12 `ProjectionPauseCommand_Http403_PrintsPermissionError` — HTTP 403 prints "Access denied" message.
  - [x] 5.13 `ProjectionPauseCommand_JsonFormat_ReturnsOperationResult` — JSON output deserializes back to `AdminOperationResult`.
  - [x] **Projection resume tests:**
  - [x] 5.14 `ProjectionResumeCommand_Success_PrintsConfirmation` — Same pattern as pause.
  - [x] 5.15 `ProjectionResumeCommand_OperationFailure_PrintsError` — Mocked 200 response with `Success=false` prints error message, exit code `2`. (Symmetric coverage with pause 5.11.)
  - [x] 5.16 `ProjectionResumeCommand_Http403_PrintsPermissionError` — HTTP 403 prints "Access denied" message.
  - [x] **Projection reset tests:**
  - [x] 5.17 `ProjectionResetCommand_Success_PrintsConfirmation` — Mocked 202 response with `Success=true` prints confirmation.
  - [x] 5.18 `ProjectionResetCommand_WithFromPosition_SendsRequestBody` — `--from 500` sends JSON body `{ "fromPosition": 500 }`.
  - [x] 5.19 `ProjectionResetCommand_WithoutFromPosition_SendsNullPosition` — No `--from` sends JSON body `{ "fromPosition": null }`.
  - [x] 5.20 `ProjectionResetCommand_Http403_PrintsPermissionError` — HTTP 403 prints permission error.
  - [x] **Argument parsing tests:**
  - [x] 5.21 `ProjectionSubcommands_MissingPositionalArgs_ReturnsError` — Missing tenant/name produces error.
  - [x] **Enum serialization tests:**
  - [x] 5.22 `ProjectionListCommand_JsonFormat_EnumsSerializeAsStrings` — `ProjectionStatusType.Running` serializes as `"running"` (not `0`). Verifies `JsonStringEnumConverter` is active.
  - [x] **URL encoding tests:**
  - [x] 5.23 `ProjectionCommands_SpecialCharsInArgs_AreUrlEncoded` — Positional arguments with special characters are URL-encoded in HTTP request path via `Uri.EscapeDataString()`.
  - [x] **PostAsync tests:**
  - [x] 5.24 `AdminApiClient_PostAsync_SendsJsonBody` — POST request includes serialized JSON body with correct Content-Type.
  - [x] 5.25 `AdminApiClient_PostAsync_Handles202AsSuccess` — HTTP 202 Accepted is deserialized as success (not treated as error).
  - [x] 5.26 `AdminApiClient_PostAsync_EmptyBody_SendsEmptyJson` — Bodyless POST sends empty content.
  - [x] 5.27 `AdminApiClient_PostAsync_Http404_ThrowsApiException` — HTTP 404 response throws `AdminApiException` with "Resource not found" message (verifies POST 404 path distinct from GET's `TryGetAsync` null-return pattern).
  - [x] **Parent command help test:**
  - [x] 5.28 `ProjectionCommand_NoSubcommand_PrintsHelp` — Running `projection` with no subcommand prints help listing all five sub-subcommands.

  Test file locations:
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Projection/ProjectionListCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Projection/ProjectionStatusCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Projection/ProjectionPauseCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Projection/ProjectionResumeCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Projection/ProjectionResetCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Client/AdminApiClientPostTests.cs`

  **Test pattern:** Reuse `MockHttpMessageHandler` from story 17-1's test project. Construct `AdminApiClient` with `internal AdminApiClient(GlobalOptions, HttpMessageHandler)` constructor. Mock responses as `HttpResponseMessage` with JSON-serialized DTOs from `Admin.Abstractions`. For POST tests, verify request body content and Content-Type header.

## Dev Notes

### Builds on story 17-1 infrastructure — do NOT recreate

All of the following already exist from story 17-1. Reuse them, do NOT recreate:
- `GlobalOptions` / `GlobalOptionsBinding` — global option parsing and resolution
- `AdminApiClient` — HTTP client with auth header, error handling, shared `JsonSerializerOptions`. **This story adds `PostAsync<TResponse>` methods to AdminApiClient.**
- `IOutputFormatter` / `OutputFormatterFactory` / `OutputWriter` — formatting infrastructure
- `ColumnDefinition` / `Alignment` — column metadata for table/CSV
- `ExitCodes` — exit code constants (Success=0, Degraded=1, Error=2)
- `JsonDefaults.Options` — shared `JsonSerializerOptions` with `JsonStringEnumConverter` (camelCase naming)
- `MockHttpMessageHandler` — test helper for HTTP mocking
- `StubCommands` — placeholder commands (will remove the projection stub)

### Admin API endpoints (already implemented in Admin.Server)

All six endpoints are implemented in `src/Hexalith.EventStore.Admin.Server/Controllers/AdminProjectionsController.cs`:

| Method | Route | Auth | Body | Returns | HTTP Status |
|--------|-------|------|------|---------|-------------|
| GET | `api/v1/admin/projections?tenantId={t}` | ReadOnly | — | `IReadOnlyList<ProjectionStatus>` | 200 |
| GET | `api/v1/admin/projections/{t}/{name}` | ReadOnly | — | `ProjectionDetail` | 200 / 404 |
| POST | `api/v1/admin/projections/{t}/{name}/pause` | Operator | — | `AdminOperationResult` | 200 |
| POST | `api/v1/admin/projections/{t}/{name}/resume` | Operator | — | `AdminOperationResult` | 200 |
| POST | `api/v1/admin/projections/{t}/{name}/reset` | Operator | `ProjectionResetRequest` | `AdminOperationResult` | 202 |
| POST | `api/v1/admin/projections/{t}/{name}/replay` | Operator | `ProjectionReplayRequest` | `AdminOperationResult` | 202 |

Note: `replay` endpoint exists but is **out of scope** for this story. It can be added as a future enhancement.

### Existing Admin.Abstractions models to reuse — do NOT recreate

All DTOs are defined in `src/Hexalith.EventStore.Admin.Abstractions/Models/`:

**Projections:**
- `ProjectionStatus(Name, TenantId, Status, Lag, Throughput, ErrorCount, LastProcessedPosition, LastProcessedUtc)` — projection list item
- `ProjectionStatusType` enum: `Running`, `Paused`, `Error`, `Rebuilding`
- `ProjectionDetail(... + Errors, Configuration, SubscribedEventTypes)` extends `ProjectionStatus` — full projection info
- `ProjectionError(Position, Timestamp, Message, EventTypeName)` — error in projection

**Common:**
- `AdminOperationResult(Success, OperationId, Message, ErrorCode)` — write operation result

Import these from the Abstractions project reference. Do NOT define new DTOs.

### Key difference from story 17-2: Write operations

Story 17-2 had only GET operations. This story introduces POST operations (pause/resume/reset). Key considerations:

1. **`AdminApiClient` needs `PostAsync`** — The existing client only has `GetAsync<T>` and `TryGetAsync<T>`. Add `PostAsync<TResponse>(string path, object? body, CancellationToken ct)` that serializes the body and sends POST. The bodyless overload `PostAsync<TResponse>(string path, CancellationToken ct)` is for pause/resume which have no request body. **Important:** the bodyless overload sends `"{}"` (empty JSON object) as the body, not null content — this ensures a consistent `Content-Type: application/json` header.

2. **HTTP 202 Accepted** — Reset returns 202, not 200. `PostAsync` must treat both 200 and 202 as success and deserialize the response body. Exit code `0` on 202 means the server accepted the request, NOT that the operation is complete. The reset confirmation message includes a follow-up hint: "Check progress: eventstore-admin projection status {tenant} {name}".

3. **Server-side error mapping** — The server's `MapOperationResult` converts `AdminOperationResult` with `ErrorCode == "NotFound"` to HTTP 404 `ProblemDetails`. The CLI's `PostAsync` should detect 404 and throw `AdminApiException` so command handlers can print subcommand-specific messages.

4. **403 Forbidden for write operations** — Write endpoints require Operator role. Users with ReadOnly tokens will get 403. The CLI should print a clear permission message.

### SEC-5: Configuration contains sensitive data

`ProjectionDetail.Configuration` is opaque JSON that may contain sensitive configuration data. The `ToString()` override on `ProjectionDetail` redacts this field. In CLI output:
- JSON format: include as-is (user explicitly requested the data)
- Table format: truncate to `MaxWidth = 80` (full value visible via `--format json`)
- CSV format: include as-is
- The CLI does NOT add its own redaction — the user chose to run the command

### Dual-section table output pattern (from HealthCommand)

The `status` subcommand uses the same dual-section pattern as `HealthCommand` from story 17-1:
```csharp
// Section 1: overview
var overview = new { Name = detail.Name, Tenant = detail.TenantId, ... };
string section1 = formatter.Format(overview, overviewColumns);

// Section 2: errors (conditional)
string section2 = "";
if (detail.Errors.Count > 0)
{
    section2 = Environment.NewLine + Environment.NewLine +
        formatter.FormatCollection(detail.Errors.ToList(), errorColumns);
}

string output = section1 + section2;
```

### List endpoint returns plain list, NOT PagedResult

Unlike `stream list` which returns `PagedResult<StreamSummary>`, the projections list endpoint returns a plain JSON array. Deserialize as `List<ProjectionStatus>` (concrete type). Do NOT use `IReadOnlyList<ProjectionStatus>` as the type parameter — `JsonSerializer.Deserialize` requires a concrete type. No pagination handling needed.

### System.CommandLine patterns — follow established conventions

Pattern from story 17-2 (use `command.SetAction`, NOT `command.SetHandler`):
```csharp
command.SetAction(async (parseResult, cancellationToken) =>
{
    GlobalOptions options = binding.Resolve(parseResult);
    string tenant = parseResult.GetValue(tenantArg);
    // ...
    return await ExecuteAsync(options, tenant, name, cancellationToken).ConfigureAwait(false);
});
```

Note: Story 17-1 used `SetAction` pattern (not `SetHandler` with `InvocationContext`). The `SetAction` callback receives `(ParseResult, CancellationToken)` and returns `Task<int>` (exit code). Follow this exact pattern.

### Folder structure for new files

```
src/Hexalith.EventStore.Admin.Cli/
  Commands/
    Projection/                              <-- NEW folder
      ProjectionCommand.cs                   <-- Parent command (replaces stub)
      ProjectionArguments.cs                 <-- Shared positional arguments
      ProjectionListCommand.cs               <-- list subcommand
      ProjectionStatusCommand.cs             <-- status subcommand (detail view)
      ProjectionPauseCommand.cs              <-- pause subcommand
      ProjectionResumeCommand.cs             <-- resume subcommand
      ProjectionResetCommand.cs              <-- reset subcommand
  Client/
    AdminApiClient.cs                        <-- MODIFIED (add PostAsync methods)

tests/Hexalith.EventStore.Admin.Cli.Tests/
  Commands/
    Projection/                              <-- NEW folder
      ProjectionListCommandTests.cs
      ProjectionStatusCommandTests.cs
      ProjectionPauseCommandTests.cs
      ProjectionResumeCommandTests.cs
      ProjectionResetCommandTests.cs
  Client/
    AdminApiClientPostTests.cs               <-- NEW file for PostAsync tests
```

### Modified files

- `src/Hexalith.EventStore.Admin.Cli/Program.cs` — replace projection stub registration with `ProjectionCommand.Create(binding)`
- `src/Hexalith.EventStore.Admin.Cli/Client/AdminApiClient.cs` — add `PostAsync<TResponse>` methods

### Architecture: CLI is a thin HTTP client (ADR-P4)

Per ADR-P4, the CLI never accesses DAPR directly. All projection queries and commands go through `AdminApiClient` -> Admin REST API -> DAPR. No DAPR SDK, no Aspire dependencies, no DI container.

### Code style — follow project conventions exactly

- File-scoped namespaces: `namespace Hexalith.EventStore.Admin.Cli.Commands.Projection;`
- Allman braces (opening brace on new line)
- Private fields: `_camelCase`
- Async methods: `Async` suffix
- 4 spaces indentation, CRLF line endings, UTF-8
- Nullable enabled
- Implicit usings enabled
- Warnings as errors

### URL encoding for route parameters

Tenant IDs and projection names may contain characters that need URL encoding. Use `Uri.EscapeDataString()` when interpolating into URL paths:
```csharp
string path = $"api/v1/admin/projections/{Uri.EscapeDataString(tenant)}/{Uri.EscapeDataString(name)}/pause";
```

### PostAsync implementation guidance

```csharp
public async Task<TResponse> PostAsync<TResponse>(string path, object? body, CancellationToken cancellationToken)
{
    string resolvedUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "unknown";
    HttpResponseMessage response;
    try
    {
        StringContent content = new(
            body is not null
                ? JsonSerializer.Serialize(body, JsonDefaults.Options)
                : "{}",
            System.Text.Encoding.UTF8,
            "application/json");
        response = await _httpClient.PostAsync(path, content, cancellationToken).ConfigureAwait(false);
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

    int statusCode = (int)response.StatusCode;
    switch (statusCode)
    {
        case 401:
            throw new AdminApiException($"Authentication required. Use --token to provide a JWT token. (URL: {resolvedUrl})");
        case 403:
            throw new AdminApiException($"Access denied. Insufficient permissions. (URL: {resolvedUrl})");
        case 404:
            throw new AdminApiException($"Resource not found at {resolvedUrl}{path}.");
        case >= 500:
            throw new AdminApiException($"Admin API server error: {statusCode}. (URL: {resolvedUrl})");
    }

    // Treat both 200 and 202 as success
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
```

### Write operation command handler pattern

All three write commands (pause/resume/reset) follow the same pattern:
```csharp
internal static async Task<int> ExecuteAsync(
    AdminApiClient client,
    GlobalOptions options,
    string tenant,
    string name,
    CancellationToken cancellationToken)
{
    IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
    OutputWriter writer = new(options.OutputFile);
    try
    {
        string path = $"api/v1/admin/projections/{Uri.EscapeDataString(tenant)}/{Uri.EscapeDataString(name)}/pause";
        AdminOperationResult result = await client
            .PostAsync<AdminOperationResult>(path, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            Console.Error.WriteLine(result.Message ?? "Operation failed.");
            return ExitCodes.Error;
        }

        Console.Error.WriteLine($"Projection '{name}' paused successfully. Operation ID: {result.OperationId}");

        string output = formatter.Format(result);
        int writeResult = writer.Write(output);
        return writeResult != ExitCodes.Success ? writeResult : ExitCodes.Success;
    }
    catch (AdminApiException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return ExitCodes.Error;
    }
}
```

### Previous story intelligence (17-1 and 17-2)

Story 17-1 established:
- `System.CommandLine` package version in `Directory.Packages.props`
- Root command structure in `Program.cs` with `GlobalOptionsBinding` pattern
- `AdminApiClient` with `GetAsync<T>` and `TryGetAsync<T>` methods
- Output formatting infrastructure (`IOutputFormatter`, `JsonOutputFormatter`, `CsvOutputFormatter`, `TableOutputFormatter`)
- `OutputWriter` for file/stdout routing
- `ExitCodes` static class
- `HealthCommand` as the working reference for dual-section output
- `StubCommands` for placeholder commands
- `MockHttpMessageHandler` test helper
- Handler wiring: `command.SetAction(async (parseResult, cancellationToken) => { ... })`

Story 17-2 established:
- `Commands/Stream/` folder structure with parent command + sub-subcommands
- `StreamArguments` shared positional argument pattern
- `StreamListCommand` as reference for collection output with filters
- `TryGetAsync<T>` on `AdminApiClient` for 404-aware GET
- `ColumnDefinition` with `MaxWidth` for large JSON fields
- URL encoding with `Uri.EscapeDataString()` for path parameters

Follow these as canonical patterns.

### JSON output field names are camelCase — critical for scriptability

JSON output uses `JsonNamingPolicy.CamelCase` via `JsonDefaults.Options`. Property names: `name`, `tenantId`, `status`, `lag`, `throughput`, `errorCount`, `lastProcessedPosition`, `lastProcessedUtc`, `errors`, `configuration`, `subscribedEventTypes`, `position`, `timestamp`, `message`, `eventTypeName`, `success`, `operationId`, `errorCode`. Operators write `jq` filters like `jq '.[] | select(.status == "running")'` — camelCase consistency is critical.

### Git commit patterns from recent work

Recent commits: `feat: <description> for story <story-id>`
Branch naming: `feat/story-17-3-projection-subcommand-list-status-pause-resume-reset`

### References

- [Source: _bmad-output/planning-artifacts/prd.md — FR79: three admin interfaces backed by shared Admin API]
- [Source: _bmad-output/planning-artifacts/prd.md — FR80: JSON/CSV/table output, exit codes, shell completions]
- [Source: _bmad-output/planning-artifacts/prd.md — NFR42: CLI startup + query within 3 seconds]
- [Source: _bmad-output/planning-artifacts/architecture.md — ADR-P4: CLI is thin HTTP client, no DAPR]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR50: subcommand tree]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR51: global options]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR52: exit codes]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Projections/ — all projection DTOs]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Common/AdminOperationResult.cs — operation result DTO]
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/AdminProjectionsController.cs — REST API endpoints]
- [Source: _bmad-output/implementation-artifacts/17-1-cli-scaffold-system-commandline-global-options.md — CLI scaffold story]
- [Source: _bmad-output/implementation-artifacts/17-2-stream-subcommand-query-list-events-state.md — stream subcommand story]

### Project Structure Notes

- New `Commands/Projection/` folder mirrors the `Commands/Stream/` structure from 17-2
- Modified `AdminApiClient.cs` in `Client/` folder adds POST capability
- No new projects or packages needed — all dependencies already exist
- Test folder `Commands/Projection/` mirrors source structure

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- Existing `internal AdminApiClient(GlobalOptions, HttpMessageHandler)` constructor reused for PostAsync testing — no new constructor needed (subtask 1.2 was already satisfied by 17-1 infrastructure).
- Pre-existing IntegrationTests build error (CS0433 ambiguous `Program` type) is unrelated to this story.

### Completion Notes List

- **Task 1:** Added `PostAsync<TResponse>(string, object?, CancellationToken)` and bodyless overload to `AdminApiClient`. Handles 200/202 as success, 401/403/404/5xx errors match `GetAsync` pattern. Created `ProjectionCommand.cs` parent, `ProjectionArguments.cs` shared args, updated `Program.cs` to replace projection stub.
- **Task 2:** Implemented `ProjectionListCommand` — GET plain list endpoint, `--tenant` filter with `-T` alias, table/JSON/CSV output, empty result handling with "No projections found." stderr message.
- **Task 3:** Implemented `ProjectionStatusCommand` — dual-section table (overview + conditional errors), `TryGetAsync` 404 handling, `MaxWidth=80` on Configuration, `MaxWidth=60` on error Message.
- **Task 4:** Implemented `ProjectionPauseCommand`, `ProjectionResumeCommand`, `ProjectionResetCommand` — POST operations via `PostAsync`, stderr confirmation messages, `AdminOperationResult` stdout formatting, `--from` option on reset with nullable `long?`.
- **Task 5:** 28 new tests across 6 test files covering all 5 subcommands, PostAsync methods, enum serialization, URL encoding, argument parsing, and parent command structure. All 94 CLI tests pass (66 existing + 28 new). All 724 Tier 1 tests pass with zero regressions.

### Code Review Fixes (2026-03-25)

3-layer adversarial code review (Blind Hunter, Edge Case Hunter, Acceptance Auditor) produced 17 raw findings → 9 actionable, 5 rejected as noise, 4 deferred as pre-existing.

**Fixes applied:**
- **AC3/4/5/8 — Subcommand-specific 403/404 messages:** Added `HttpStatusCode` property to `AdminApiException`. Pause/resume/reset handlers now switch on status code to print `"Access denied. Operator role required to {verb} projections."` (403) and `"Projection '{name}' not found in tenant '{tenant}'."` (404) instead of generic messages.
- **Resource leak — `HttpResponseMessage` not disposed in `PostAsync`:** Wrapped post-request section in `using (response) { ... }`, matching `TryGetAsync` pattern.
- **URL formatting — double-slash in 404 error message:** Changed `{resolvedUrl}/{path}` to `{resolvedUrl}{path}` matching `GetAsync` pattern.
- **AC2 — CSV no-errors path:** Changed from returning empty string to rendering overview as a single CSV row (AC2 governs over contradicting Task 3.1 subtask).
- **AC9 — Test gaps:** Added 3 new HTTP 404 tests for pause/resume/reset command handlers. Added `HttpStatusCode` assertions to `AdminApiClientPostTests`.

**Deferred (pre-existing from story 17-1, not caused by this change):**
- `GetAsync` also doesn't dispose `HttpResponseMessage` (only `TryGetAsync` does)
- Non-handled 4xx status codes (400/408/409/429) throw `HttpRequestException` instead of `AdminApiException`
- Duplicate `HttpRequestException` catch blocks produce identical messages
- `OutputWriter` not wrapped in `using` in command handlers

### File List

- `src/Hexalith.EventStore.Admin.Cli/Client/AdminApiException.cs` — MODIFIED (added HttpStatusCode property + constructor)
- `src/Hexalith.EventStore.Admin.Cli/Client/AdminApiClient.cs` — MODIFIED (added PostAsync methods, response disposal, URL fix, status codes on exceptions)
- `src/Hexalith.EventStore.Admin.Cli/Program.cs` — MODIFIED (replaced projection stub with ProjectionCommand)
- `src/Hexalith.EventStore.Admin.Cli/Commands/Projection/ProjectionCommand.cs` — NEW (parent command)
- `src/Hexalith.EventStore.Admin.Cli/Commands/Projection/ProjectionArguments.cs` — NEW (shared positional arguments)
- `src/Hexalith.EventStore.Admin.Cli/Commands/Projection/ProjectionListCommand.cs` — NEW (list subcommand)
- `src/Hexalith.EventStore.Admin.Cli/Commands/Projection/ProjectionStatusCommand.cs` — NEW (status subcommand, CSV overview row fix)
- `src/Hexalith.EventStore.Admin.Cli/Commands/Projection/ProjectionPauseCommand.cs` — NEW (pause subcommand, subcommand-specific 403/404)
- `src/Hexalith.EventStore.Admin.Cli/Commands/Projection/ProjectionResumeCommand.cs` — NEW (resume subcommand, subcommand-specific 403/404)
- `src/Hexalith.EventStore.Admin.Cli/Commands/Projection/ProjectionResetCommand.cs` — NEW (reset subcommand, subcommand-specific 403/404)
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Projection/ProjectionListCommandTests.cs` — NEW (9 tests)
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Projection/ProjectionStatusCommandTests.cs` — NEW (5 tests)
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Projection/ProjectionPauseCommandTests.cs` — NEW (5 tests)
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Projection/ProjectionResumeCommandTests.cs` — NEW (4 tests)
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Projection/ProjectionResetCommandTests.cs` — NEW (5 tests)
- `tests/Hexalith.EventStore.Admin.Cli.Tests/Client/AdminApiClientPostTests.cs` — NEW (7 tests)

## Change Log

- **2026-03-25:** Story 17-3 implementation — Added 5 projection subcommands (list, status, pause, resume, reset) with PostAsync support on AdminApiClient. 28 new tests, all 94 CLI tests and 724 Tier 1 tests pass with zero regressions.
- **2026-03-25:** Code review fixes — 3-layer adversarial review (17 findings → 9 fixed, 4 deferred, 5 rejected). Added HttpStatusCode to AdminApiException for subcommand-specific 403/404 messages, fixed HttpResponseMessage disposal in PostAsync, fixed 404 URL double-slash, fixed CSV no-errors path per AC2, added 4 new tests. All 98 CLI tests pass with zero warnings.
