# Story 17.5: Tenant Subcommand — List, Quotas, Verify

Status: done

Size: Medium — ~14 new files (8 source, 6 test), 5 task groups, 11 ACs, ~29 tests (~8-10 hours estimated). Replaces the `tenant` stub from story 17-1 with six sub-subcommands (`list`, `detail`, `quotas`, `users`, `compare`, `verify`) that call the existing Admin API tenant endpoints. Reuses output formatting, `AdminApiClient`, `GlobalOptionsBinding`, and exit code infrastructure from 17-1. Read-only operations only — no write operations (create, disable, enable, add-user) in this story.

**Dependency:** Story 17-1 must be complete (done). This story builds on the CLI scaffold, global options, output formatting, `AdminApiClient`, and exit code conventions established there. Stories 17-2, 17-3, and 17-4 patterns are followed but their completion is not a prerequisite.

## Definition of Done

- All 11 ACs verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- All six `tenant` sub-subcommands return formatted output with correct exit codes
- `eventstore-admin tenant list` returns a table of tenant summaries
- `eventstore-admin tenant detail <tenantId>` displays tenant details
- `eventstore-admin tenant quotas <tenantId>` displays quota usage with percentage
- `eventstore-admin tenant users <tenantId>` displays tenant users
- `eventstore-admin tenant compare <tenantId1> [tenantId2] ...` compares tenant usage side-by-side
- `eventstore-admin tenant verify <tenantId>` validates tenant health and quota compliance with CI/CD exit codes

## Story

As a **platform operator or DBA**,
I want **`eventstore-admin tenant` sub-subcommands to list tenants, inspect tenant details and quotas, view users, compare tenant usage, and verify tenant health**,
so that **I can monitor tenant status, troubleshoot quota issues, compare resource consumption across tenants, and use verification as a CI/CD gate for tenant compliance checks without needing the Web UI (FR77, FR79, FR80, NFR42)**.

## Acceptance Criteria

1. **`tenant list` subcommand** — `eventstore-admin tenant list` calls `GET /api/v1/admin/tenants` and displays a table of `TenantSummary` records. Columns: Tenant ID (TenantId), Display Name (DisplayName), Status (Status), Events (EventCount, right-aligned), Domains (DomainCount, right-aligned). Exit code `0` on success, `2` on error. Empty result set returns exit code `0` with "No tenants found." message to stderr. Deserialize as `List<TenantSummary>` (concrete type — plain JSON array). JSON format: serialize the entire list. CSV format: same columns as table.

2. **`tenant detail` subcommand** — `eventstore-admin tenant detail <tenantId>` calls `GET /api/v1/admin/tenants/{tenantId}` and displays `TenantDetail`. For `table` format (dual-section, same pattern as `HealthCommand` and `ProjectionStatusCommand`):
   - **Section 1 — Overview:** Key/value pairs: Tenant ID, Display Name, Status, Events (EventCount), Domains (DomainCount), Storage (StorageBytes), Created (CreatedAtUtc), Subscription Tier (SubscriptionTier).
   - **Section 2 — Quotas:** Only rendered when `Quotas` is non-null. Key/value pairs: Max Events/Day (MaxEventsPerDay), Max Storage (MaxStorageBytes), Current Usage (CurrentUsage).
   For `json` format: full `TenantDetail` serialized. For `csv` format: overview key/value as single CSV row. HTTP 404 prints "Tenant '{tenantId}' not found." to stderr, exit code `2`.

3. **`tenant quotas` subcommand** — `eventstore-admin tenant quotas <tenantId>` calls `GET /api/v1/admin/tenants/{tenantId}/quotas` and displays `TenantQuotas`. For `table` format: key/value pairs: Tenant ID, Max Events/Day, Max Storage (MaxStorageBytes), Current Usage (CurrentUsage), Usage Percentage (calculated as `CurrentUsage * 100 / MaxStorageBytes`, formatted as `{pct}%`). Exit code: `0` if usage percentage < 90%, `1` if >= 90% (warning), `2` on error or HTTP 404. For `json` format: full `TenantQuotas` serialized. **404 handling strategy:** Use `TryGetAsync<TenantQuotas>` — returns null on HTTP 404. Null means the tenant does not exist (the server returns 404 for unknown tenants, not for tenants without quotas — all tenants have quotas). When null: print "Tenant '{tenantId}' not found." to stderr, exit code `2`.

4. **`tenant users` subcommand** — `eventstore-admin tenant users <tenantId>` calls `GET /api/v1/admin/tenants/{tenantId}/users` and displays a table of `TenantUser` records. Columns: Email (Email), Role (Role), Added (AddedAtUtc). Exit code `0` on success, `2` on error. Empty result set returns exit code `0` with "No users found for tenant '{tenantId}'." to stderr. HTTP 404: "Tenant '{tenantId}' not found." to stderr, exit code `2`. JSON format: serialize the entire list. CSV format: same columns as table. **404 handling strategy:** Use `TryGetAsync<List<TenantUser>>` — returns null on HTTP 404 (tenant not found), returns an empty `List<TenantUser>` when tenant exists but has no users. This cleanly distinguishes "tenant not found" (null → exit code 2) from "no users" (empty list → exit code 0), consistent with the `detail` and `quotas` subcommands.

5. **`tenant compare` subcommand** — `eventstore-admin tenant compare <tenantIds...>` accepts a variadic argument (`Argument<string[]>`) for tenant IDs (minimum 2). Calls `POST /api/v1/admin/tenants/compare` with JSON body `{ "tenantIds": ["id1", "id2", ...] }` and displays `TenantComparison`. For `table` format: table of `TenantSummary` records from `TenantComparison.Tenants` using the same columns as `tenant list`, plus a footer line "Compared at: {ComparedAtUtc}". For `json` format: full `TenantComparison` serialized. Exit code `0` on success, `2` on error. Validation: `ArgumentArity(2, int.MaxValue)` enforces minimum 2 tenant IDs — System.CommandLine produces a built-in error message when fewer are provided. No additional `AddValidator` needed.

6. **`tenant verify` subcommand** — `eventstore-admin tenant verify <tenantId>` performs an operational compliance check on a single tenant (status active, quota usage within limits). **Note:** This checks operational health, not data isolation — tenant data isolation is structural (AggregateIdentity key patterns, DAPR policies) and verified by integration tests, not a CLI command. Calls two endpoints sequentially:
   1. `GET /api/v1/admin/tenants/{tenantId}` — retrieve `TenantDetail` (404 = tenant not found, exit code `2`).
   2. `GET /api/v1/admin/tenants/{tenantId}/quotas` — retrieve `TenantQuotas`.
   For `table` format: key/value pairs: Tenant ID, Status, Subscription Tier, Events, Storage (StorageBytes), Max Events/Day, Max Storage (MaxStorageBytes), Current Usage (CurrentUsage), Usage Percentage (`{pct}%`), Verdict. Verdict logic:
   - `PASS` (exit code `0`): tenant status is `Active` AND usage percentage < 90%.
   - `WARNING` (exit code `1`): tenant status is `Active` AND usage percentage >= 90%.
   - `FAIL` (exit code `2`): tenant status is `Suspended` or `Onboarding`, OR usage percentage >= 100%.
   For `json` format: serialize the flat `TenantVerifyResult` record — produces `{ "tenantId": "...", "displayName": "...", "status": "active", "subscriptionTier": "...", "eventCount": 1234, "storageBytes": 5678, "maxEventsPerDay": 100000, "maxStorageBytes": 1073741824, "currentUsage": 536870912, "usagePercentage": 50, "verdict": "PASS" }`. Flat structure is simpler for `jq` pipelines than nested objects. This subcommand is designed for CI/CD gates: `eventstore-admin tenant verify acme-corp --quiet` returns only an exit code.

7. **`--quiet` / `-q` option on `tenant verify`** — `eventstore-admin tenant verify <tenantId> --quiet` suppresses all stdout output. Only the exit code is meaningful. Errors still go to stderr. This enables the CI/CD pattern: `eventstore-admin tenant verify acme-corp --quiet` checks tenant compliance silently. `--quiet` with `--output <file>` still writes to the file (stdout suppressed, file preserved). `--quiet` is local to `verify` only — not on other tenant subcommands.

8. **Positional arguments** — `tenantId` is `Argument<string>` (positional, required) on `detail`, `quotas`, `users`, and `verify`. `tenantIds` is `Argument<string[]>` (positional, variadic, required, arity minimum 2) on `compare`. `list` has no positional arguments. Missing required arguments produce `System.CommandLine`'s built-in error message and usage help.

9. **Error handling** — All sub-subcommands reuse `AdminApiClient` error handling from story 17-1 for HTTP 401/403/5xx, connection refused, timeout, and JSON deserialization failures. **Per-subcommand 404 strategy:** `detail`, `quotas`, and `users` use `TryGetAsync<T>` (returns null on 404 — tenant not found). `compare` uses `PostAsync<TResponse>` (throws `AdminApiException` on error). `verify` uses sequential `TryGetAsync` calls with early exit on null. All errors go to stderr, exit code `2`.

10. **Parent `tenant` command help** — Running `eventstore-admin tenant` with no subcommand prints help listing all six sub-subcommands (`list`, `detail`, `quotas`, `users`, `compare`, `verify`) with their descriptions. `System.CommandLine` provides this automatically for parent commands with no handler.

11. **Test coverage** — Unit tests cover: each sub-subcommand's output formatting (table, JSON, CSV), exit code mapping (success/error/warning), HTTP 404 handling, HTTP 401 unauthorized error, positional argument parsing, enum serialization as strings (not integers), URL encoding of special characters in tenant IDs, `PostAsync` for compare, compare API error handling, verify verdict logic (PASS/WARNING/FAIL), verify with null quotas (tenant exists but quotas endpoint returns null), verify quiet mode, empty list handling, and CSV format for collection subcommands. **Note:** All numeric values (bytes, event counts) are output as raw numbers — no human-readable formatting. This is consistent with existing CLI commands (stream, projection, health) and better for JSON/CSV scriptability. Human-readable byte formatting is a future CLI-wide enhancement.

## Tasks / Subtasks

- [x] **Task 1: Create parent command and shared arguments** (AC: 8, 10)
  - [x] 1.1 Create `Commands/Tenant/TenantCommand.cs` — parent command with no handler:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli.Commands.Tenant;
    public static class TenantCommand
    {
        public static Command Create(GlobalOptionsBinding binding)
        {
            Command command = new("tenant", "List tenants, view quotas, and verify isolation");
            command.Subcommands.Add(TenantListCommand.Create(binding));
            command.Subcommands.Add(TenantDetailCommand.Create(binding));
            command.Subcommands.Add(TenantQuotasCommand.Create(binding));
            command.Subcommands.Add(TenantUsersCommand.Create(binding));
            command.Subcommands.Add(TenantCompareCommand.Create(binding));
            command.Subcommands.Add(TenantVerifyCommand.Create(binding));
            return command;
        }
    }
    ```
  - [x] 1.2 Create `Commands/Tenant/TenantArguments.cs` — shared positional arguments:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli.Commands.Tenant;
    public static class TenantArguments
    {
        public static Argument<string> TenantId() => new("tenantId", "Tenant identifier");
    }
    ```
  - [x] 1.3 Update `Program.cs` — replace `StubCommands.Create("tenant", ...)` with `TenantCommand.Create(binding)`. Add `using Hexalith.EventStore.Admin.Cli.Commands.Tenant;`. Keep all other stubs unchanged.

- [x] **Task 2: `tenant list` subcommand** (AC: 1)
  - [x] 2.1 Create `Commands/Tenant/TenantListCommand.cs`:
    - No arguments or options (lists all tenants).
    - Handler: resolves global options via `binding.Resolve(parseResult)`, creates `AdminApiClient`, calls `GET /api/v1/admin/tenants`.
    - Deserializes as `List<TenantSummary>` (concrete type — plain JSON array).
    - Column definitions:
    ```csharp
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Tenant ID", "TenantId"),
        new("Display Name", "DisplayName"),
        new("Status", "Status"),
        new("Events", "EventCount", Align: Alignment.Right),
        new("Domains", "DomainCount", Align: Alignment.Right),
    ];
    ```
    - When list is empty: print "No tenants found." to stderr, exit code `0`.
    - Formats via `FormatCollection` with `Columns`.
    - Two `ExecuteAsync` overloads: public (creates client) and internal (accepts client for testing).
    ```csharp
    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        CancellationToken cancellationToken)
    ```

- [x] **Task 3: `tenant detail` and `tenant quotas` subcommands** (AC: 2, 3)
  - [x] 3.1 Create `Commands/Tenant/TenantDetailCommand.cs`:
    - One positional argument: `TenantArguments.TenantId()`.
    - Handler: calls `TryGetAsync<TenantDetail>("api/v1/admin/tenants/{tenantId}")`.
    - If null: "Tenant '{tenantId}' not found." to stderr, exit code `2`.
    - For `table` format (dual-section like `HealthCommand`):
      - Section 1 — Overview columns:
      ```csharp
      internal static readonly List<ColumnDefinition> OverviewColumns =
      [
          new("Tenant ID", "TenantId"),
          new("Display Name", "DisplayName"),
          new("Status", "Status"),
          new("Events", "EventCount"),
          new("Domains", "DomainCount"),
          new("Storage", "StorageBytes"),
          new("Created", "CreatedAtUtc"),
          new("Subscription Tier", "SubscriptionTier"),
      ];
      ```
      - Section 2 — Quotas columns (only when `Quotas` is non-null):
      ```csharp
      internal static readonly List<ColumnDefinition> QuotaColumns =
      [
          new("Max Events/Day", "MaxEventsPerDay"),
          new("Max Storage", "MaxStorageBytes"),
          new("Current Usage", "CurrentUsage"),
      ];
      ```
    - For `json` format: full `TenantDetail` serialized.
    - Testable overload:
    ```csharp
    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenantId,
        CancellationToken cancellationToken)
    ```
  - [x] 3.2 Create `Commands/Tenant/TenantQuotasCommand.cs`:
    - One positional argument: `TenantArguments.TenantId()`.
    - Handler: calls `TryGetAsync<TenantQuotas>("api/v1/admin/tenants/{tenantId}/quotas")`.
    - If null: "Tenant '{tenantId}' not found." to stderr, exit code `2`.
    - Calculate usage percentage: `int usagePct = (int)(quotas.CurrentUsage * 100 / Math.Max(quotas.MaxStorageBytes, 1))`.
    - For `table` format: key/value pairs rendered via `Format` (single-object) with columns:
    ```csharp
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Tenant ID", "TenantId"),
        new("Max Events/Day", "MaxEventsPerDay"),
        new("Max Storage", "MaxStorageBytes"),
        new("Current Usage", "CurrentUsage"),
    ];
    ```
    - Print usage percentage to stderr: `"Storage usage: {usagePct}%"`.
    - Exit code logic:
    ```csharp
    static int MapExitCode(int usagePercentage) => usagePercentage switch
    {
        >= 90 => ExitCodes.Degraded,  // Warning: approaching quota
        _ => ExitCodes.Success,
    };
    ```
    - Testable overload:
    ```csharp
    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenantId,
        CancellationToken cancellationToken)
    ```

- [x] **Task 4: `tenant users`, `tenant compare`, and `tenant verify` subcommands** (AC: 4, 5, 6, 7)
  - [x] 4.1 Create `Commands/Tenant/TenantUsersCommand.cs`:
    - One positional argument: `TenantArguments.TenantId()`.
    - Handler: calls `TryGetAsync<List<TenantUser>>("api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}/users")`.
    - **404 handling:** `TryGetAsync` returns null on HTTP 404 (tenant not found), returns empty `List<TenantUser>` when tenant exists but has no users. When null: print "Tenant '{tenantId}' not found." to stderr, exit code `2`. When empty list: print "No users found for tenant '{tenantId}'." to stderr, exit code `0`. This is consistent with `detail` and `quotas` subcommands.
    - Column definitions:
    ```csharp
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Email", "Email"),
        new("Role", "Role"),
        new("Added", "AddedAtUtc"),
    ];
    ```
    - Formats via `FormatCollection` with `Columns`.
    - Testable overload:
    ```csharp
    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenantId,
        CancellationToken cancellationToken)
    ```
  - [x] 4.2 Create `Commands/Tenant/TenantCompareCommand.cs`:
    - One variadic argument: `Argument<string[]>("tenantIds", "Tenant IDs to compare") { Arity = new ArgumentArity(2, int.MaxValue) }`. The arity constraint handles minimum validation natively — no `AddValidator` needed.
    - Handler: calls `PostAsync<TenantComparison>("api/v1/admin/tenants/compare", new { TenantIds = tenantIds }, cancellationToken)`. Uses an anonymous object — `TenantCompareRequest` is a server-side model in `Admin.Server.Models` that the CLI must NOT reference. `JsonDefaults.Options` (camelCase) serializes this as `{ "tenantIds": [...] }`.
    - **Null guard on variadic argument:** `parseResult.GetValue(tenantIdsArg)` returns `string[]?`. Add null check before calling `ExecuteAsync`:
    ```csharp
    string[] tenantIds = parseResult.GetValue(tenantIdsArg) ?? [];
    ```
    The `ArgumentArity(2, int.MaxValue)` validator prevents this in practice, but the null check satisfies nullable analysis.
    - For `table` format: `FormatCollection` with `TenantListCommand.Columns` (reuse tenant list columns) then print footer `"Compared at: {comparison.ComparedAtUtc}"` to stderr.
    - For `json` format: full `TenantComparison` serialized.
    - Exit code `0` on success, `2` on error.
    - Testable overload:
    ```csharp
    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string[] tenantIds,
        CancellationToken cancellationToken)
    ```
  - [x] 4.3 Create `Commands/Tenant/TenantVerifyCommand.cs`:
    - One positional argument: `TenantArguments.TenantId()`.
    - One local option: `--quiet` / `-q` (`Option<bool>`, default `false`).
    - Handler: resolves global options and `--quiet`, then calls `ExecuteAsync`.
    - Implementation:
    ```csharp
    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string tenantId,
        bool quiet,
        CancellationToken cancellationToken)
    {
        // Step 1: Get tenant detail
        TenantDetail? detail = await client
            .TryGetAsync<TenantDetail>($"api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}", cancellationToken)
            .ConfigureAwait(false);
        if (detail is null)
        {
            await Console.Error.WriteLineAsync($"Tenant '{tenantId}' not found.").ConfigureAwait(false);
            return ExitCodes.Error;
        }

        // Step 2: Get quotas
        TenantQuotas? quotas = await client
            .TryGetAsync<TenantQuotas>($"api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}/quotas", cancellationToken)
            .ConfigureAwait(false);

        // Step 3: Calculate verdict
        int usagePct = quotas is not null
            ? (int)(quotas.CurrentUsage * 100 / Math.Max(quotas.MaxStorageBytes, 1))
            : 0;
        string verdict = DeriveVerdict(detail.Status, usagePct);
        int exitCode = MapExitCode(verdict);

        // Step 4: Output
        if (!quiet || options.OutputFile is not null)
        {
            // Format and output...
        }
        return exitCode;
    }

    internal static string DeriveVerdict(TenantStatusType status, int usagePercentage) =>
        status switch
        {
            TenantStatusType.Suspended => "FAIL",
            TenantStatusType.Onboarding => "FAIL",
            _ when usagePercentage >= 100 => "FAIL",
            _ when usagePercentage >= 90 => "WARNING",
            _ => "PASS",
        };

    internal static int MapExitCode(string verdict) => verdict switch
    {
        "PASS" => ExitCodes.Success,
        "WARNING" => ExitCodes.Degraded,
        _ => ExitCodes.Error,
    };
    ```
    - For `table` format: key/value pairs with columns:
    ```csharp
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Tenant ID", "TenantId"),
        new("Status", "Status"),
        new("Subscription Tier", "SubscriptionTier"),
        new("Events", "EventCount"),
        new("Storage", "StorageBytes"),
        new("Max Events/Day", "MaxEventsPerDay"),
        new("Max Storage", "MaxStorageBytes"),
        new("Current Usage", "CurrentUsage"),
        new("Usage %", "UsagePercentage"),
        new("Verdict", "Verdict"),
    ];
    ```
    Since the verify output is a composite of `TenantDetail` + `TenantQuotas` + computed fields, create an anonymous object or a small internal record for formatting:
    ```csharp
    internal record TenantVerifyResult(
        string TenantId,
        string DisplayName,
        TenantStatusType Status,
        string? SubscriptionTier,
        long EventCount,
        long StorageBytes,
        long MaxEventsPerDay,
        long MaxStorageBytes,
        long CurrentUsage,
        int UsagePercentage,
        string Verdict);
    ```
    - For `json` format: serialize the `TenantVerifyResult` record.
    - `--quiet` without `--output`: skip output formatting entirely. `--quiet` with `--output <file>`: format and write to file (suppress stdout).

- [x] **Task 5: Unit tests** (AC: 11)
  - [x] **Tenant list tests** (`TenantListCommandTests.cs`):
  - [x] 5.1 `TenantListCommand_ReturnsTable` — Table output contains expected headers and data.
  - [x] 5.2 `TenantListCommand_EmptyResult_PrintsNoTenantsFound` — Empty list prints "No tenants found." to stderr, exit code `0`.
  - [x] 5.3 `TenantListCommand_JsonFormat_ReturnsValidJson` — JSON output deserializes back to `List<TenantSummary>`.
  - [x] 5.4 `TenantListCommand_CsvFormat_ReturnsHeaderAndRows` — CSV has header + data rows.
  - [x] 5.5 `TenantListCommand_EnumsSerializeAsStrings` — `TenantStatusType.Active` serializes as `"active"`, not `0`.
  - [x] 5.6 `TenantListCommand_Unauthorized_ReturnsError` — HTTP 401 (missing token) throws `AdminApiException`, exit code `2`. Tests that `AdminApiClient` generic auth error handling propagates correctly through tenant commands.
  - [x] **Tenant detail tests** (`TenantDetailCommandTests.cs`):
  - [x] 5.7 `TenantDetailCommand_ReturnsTenantInfo` — Successful response displays tenant info, exit code `0`.
  - [x] 5.8 `TenantDetailCommand_NotFound_ReturnsError` — HTTP 404 prints "Tenant 'xyz' not found." to stderr, exit code `2`.
  - [x] 5.9 `TenantDetailCommand_JsonFormat_ReturnsFullDetail` — JSON output deserializes back to `TenantDetail`.
  - [x] 5.10 `TenantDetailCommand_SpecialCharsInTenantId_AreUrlEncoded` — Tenant ID with spaces/slashes is URL-encoded in the request URI. Verify via `handler.LastRequest.RequestUri`.
  - [x] **Tenant quotas tests** (`TenantQuotasCommandTests.cs`):
  - [x] 5.11 `TenantQuotasCommand_ReturnsQuotas` — Successful response displays quotas, exit code `0`.
  - [x] 5.12 `TenantQuotasCommand_HighUsage_ReturnsWarning` — Usage >= 90% returns exit code `1`.
  - [x] 5.13 `TenantQuotasCommand_NotFound_ReturnsError` — HTTP 404, exit code `2`.
  - [x] **Tenant users tests** (`TenantUsersCommandTests.cs`):
  - [x] 5.14 `TenantUsersCommand_ReturnsUserTable` — Table output with Email, Role, Added columns.
  - [x] 5.15 `TenantUsersCommand_EmptyResult_PrintsNoUsersFound` — Empty list, exit code `0`.
  - [x] 5.16 `TenantUsersCommand_NotFound_ReturnsError` — HTTP 404 (via `TryGetAsync` returning null), exit code `2`.
  - [x] 5.17 `TenantUsersCommand_CsvFormat_ReturnsHeaderAndRows` — CSV has header + data rows for users collection.
  - [x] **Tenant compare tests** (`TenantCompareCommandTests.cs`):
  - [x] 5.18 `TenantCompareCommand_ReturnsComparisonTable` — Table output with tenant rows and footer.
  - [x] 5.19 `TenantCompareCommand_JsonFormat_ReturnsFullComparison` — JSON output includes `ComparedAtUtc`.
  - [x] 5.20 `TenantCompareCommand_SendsCorrectPostBody` — Verify POST body contains `tenantIds` array.
  - [x] 5.21 `TenantCompareCommand_ApiError_ReturnsError` — Server returns 5xx or `AdminApiException` → exit code `2`.
  - [x] **Tenant verify tests** (`TenantVerifyCommandTests.cs`):
  - [x] 5.22 `TenantVerifyCommand_ActiveLowUsage_ReturnsPass` — Active tenant with < 90% usage → exit code `0`, verdict "PASS".
  - [x] 5.23 `TenantVerifyCommand_ActiveHighUsage_ReturnsWarning` — Active tenant with >= 90% usage → exit code `1`, verdict "WARNING".
  - [x] 5.24 `TenantVerifyCommand_Suspended_ReturnsFail` — Suspended tenant → exit code `2`, verdict "FAIL".
  - [x] 5.25 `TenantVerifyCommand_OverQuota_ReturnsFail` — Usage >= 100% → exit code `2`, verdict "FAIL".
  - [x] 5.26 `TenantVerifyCommand_NotFound_ReturnsError` — HTTP 404, exit code `2`.
  - [x] 5.27 `TenantVerifyCommand_NullQuotas_DefaultsToPass` — Tenant exists but quotas endpoint returns null (no quotas configured). Usage defaults to 0%, verdict "PASS", exit code `0`. Validates the `quotas is not null ? ... : 0` fallback.
  - [x] 5.28 `TenantVerifyCommand_Quiet_SuppressesStdout` — With `--quiet`, stdout is empty. Exit code matches verdict.
  - [x] 5.29 `TenantVerifyCommand_JsonFormat_ReturnsCompositeObject` — JSON output includes verdict and usagePercentage.

  Test file locations:
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Tenant/TenantListCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Tenant/TenantDetailCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Tenant/TenantQuotasCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Tenant/TenantUsersCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Tenant/TenantCompareCommandTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Tenant/TenantVerifyCommandTests.cs`

  **Test pattern:** Reuse `MockHttpMessageHandler` from the test project. For `verify` tests that make multiple sequential API calls, use `QueuedMockHttpMessageHandler` (from story 17-4) that returns different responses on successive calls. Construct `AdminApiClient` with `internal AdminApiClient(GlobalOptions, HttpMessageHandler)` constructor. For 404 tests: return `HttpStatusCode.NotFound` — `TryGetAsync` returns null on 404, which the command handlers check for. For 401 test: return `HttpStatusCode.Unauthorized` — `GetAsync`/`TryGetAsync` both throw `AdminApiException` on 401.

## Dev Notes

### Builds on story 17-1 infrastructure — do NOT recreate

All of the following already exist from previous stories. Reuse them:
- `GlobalOptions` / `GlobalOptionsBinding` — global option parsing and resolution
- `AdminApiClient` — HTTP client with auth, error handling, shared `JsonSerializerOptions`. Includes `GetAsync<T>`, `TryGetAsync<T>`, `PostAsync<TResponse>`. **No changes needed to AdminApiClient for this story.**
- `IOutputFormatter` / `OutputFormatterFactory` / `OutputWriter` — formatting infrastructure
- `ColumnDefinition` / `Alignment` — column metadata for table/CSV
- `ExitCodes` — exit code constants (Success=0, Degraded=1, Error=2)
- `JsonDefaults.Options` — shared `JsonSerializerOptions` with `JsonStringEnumConverter` (camelCase naming)
- `MockHttpMessageHandler` — test helper for HTTP mocking (single response)
- `QueuedMockHttpMessageHandler` — test helper for sequential responses (from story 17-4)

### Admin API endpoints used

All endpoints already exist in `src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs`:

| Method | Route | Auth | Returns | HTTP Status |
|--------|-------|------|---------|-------------|
| GET | `api/v1/admin/tenants` | ReadOnly | `IReadOnlyList<TenantSummary>` | 200 |
| GET | `api/v1/admin/tenants/{tenantId}` | ReadOnly | `TenantDetail` | 200 / 404 |
| GET | `api/v1/admin/tenants/{tenantId}/quotas` | ReadOnly | `TenantQuotas` | 200 / 404 |
| GET | `api/v1/admin/tenants/{tenantId}/users` | ReadOnly | `IReadOnlyList<TenantUser>` | 200 / 404 |
| POST | `api/v1/admin/tenants/compare` | ReadOnly | `TenantComparison` | 200 |

### Existing Admin.Abstractions models — do NOT recreate

All DTOs are defined in `src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/`:

- `TenantSummary(TenantId, DisplayName, Status, EventCount, DomainCount)` — list view
- `TenantDetail(TenantId, DisplayName, Status, EventCount, DomainCount, StorageBytes, CreatedAtUtc, Quotas, SubscriptionTier)` — full detail
- `TenantQuotas(TenantId, MaxEventsPerDay, MaxStorageBytes, CurrentUsage)` — quota limits/usage
- `TenantStatusType` enum: `Active`, `Suspended`, `Onboarding`
- `TenantUser(Email, Role, AddedAtUtc)` — user assignment
- `TenantComparison(Tenants, ComparedAtUtc)` — comparison result

### TenantCompareRequest — server-side model, do NOT reference from CLI

`TenantCompareRequest(IReadOnlyList<string> TenantIds)` is defined in `src/Hexalith.EventStore.Admin.Server/Models/`. The CLI project does NOT reference `Admin.Server`. Instead, use an anonymous object:
```csharp
object body = new { TenantIds = tenantIds };
```
`JsonDefaults.Options` (camelCase naming) serializes this as `{ "tenantIds": ["a", "b"] }`, matching the server's expected format.

### Dual-section table output pattern (from HealthCommand, ProjectionStatusCommand)

For `detail` and `verify` subcommands that show key/value + table sections:
```csharp
IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
string section1 = formatter.Format(overviewData, OverviewColumns);
OutputWriter.Write(section1, options.OutputFile);
if (hasQuotas)
{
    string section2 = formatter.Format(quotaData, QuotaColumns);
    OutputWriter.Write(section2, options.OutputFile);
}
```

For `json` format, serialize the full object (both sections combined).

### verify subcommand — composite object pattern

The `verify` subcommand creates a composite result from two API calls. Define a small internal record:
```csharp
internal record TenantVerifyResult(
    string TenantId,
    string DisplayName,
    TenantStatusType Status,
    string? SubscriptionTier,
    long EventCount,
    long StorageBytes,
    long MaxEventsPerDay,
    long MaxStorageBytes,
    long CurrentUsage,
    int UsagePercentage,
    string Verdict);
```

Construction:
```csharp
int usagePct = quotas is not null
    ? (int)(quotas.CurrentUsage * 100 / Math.Max(quotas.MaxStorageBytes, 1))
    : 0;
string verdict = DeriveVerdict(detail.Status, usagePct);

TenantVerifyResult result = new(
    detail.TenantId,
    detail.DisplayName,
    detail.Status,
    detail.SubscriptionTier,
    detail.EventCount,
    detail.StorageBytes,
    quotas?.MaxEventsPerDay ?? 0,
    quotas?.MaxStorageBytes ?? 0,
    quotas?.CurrentUsage ?? 0,
    usagePct,
    verdict);
```

### URL encoding — use Uri.EscapeDataString for tenant IDs

Tenant IDs are user-provided and may contain special characters. Always escape:
```csharp
string path = $"api/v1/admin/tenants/{Uri.EscapeDataString(tenantId)}";
```

### verify --quiet pattern (from HealthCommand story 17-4)

Follow the `--quiet` pattern from `HealthCommand`:
```csharp
if (!quiet || options.OutputFile is not null)
{
    IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);
    string output = formatter.Format(result, Columns);
    OutputWriter.Write(output, options.OutputFile, quiet);
}
```
When quiet is true and `OutputFile` is null: skip output entirely. When quiet is true but `OutputFile` is set: write to file (suppress stdout).

### Tenant management context — Hexalith.Tenants peer service

Per sprint change proposal (2026-03-21), tenant lifecycle is managed by the Hexalith.Tenants peer service. The Admin API (AdminTenantsController) proxies tenant operations via DAPR service invocation to the `tenants` app ID. The CLI is a thin HTTP client (ADR-P4) — it calls the Admin API, which delegates to Hexalith.Tenants. No DAPR SDK, no Aspire dependencies, no Hexalith.Tenants.Client packages in the CLI.

**Note on sprint proposal wording:** The sprint change proposal (2026-03-21) says "CLI tenant subcommands call Hexalith.Tenants CommandApi directly via its client packages." This was aspirational guidance written before Epic 14 implementation. In practice, the Admin API (Epic 14, stories 14-2/14-3) already built the proxy layer (`DaprTenantQueryService`/`DaprTenantCommandService` → `AdminTenantsController`). Per ADR-P4, the CLI goes through Admin API, not directly to Hexalith.Tenants. This story follows ADR-P4 — no Hexalith.Tenants.Client dependency in the CLI.

### Verify subcommand — future optimization note

The `verify` subcommand makes two sequential HTTP calls (detail + quotas). For monitoring scripts polling frequently (e.g., every 30s), this doubles round-trips. A dedicated `GET /api/v1/admin/tenants/{id}/verify` composite endpoint on the Admin Server would reduce to one call. Out of scope — note for Epic 17 retrospective.

### Future enhancements (out of scope for this story)

- **Write operations:** `tenant create`, `tenant disable`, `tenant enable`, `tenant add-user`, `tenant remove-user`, `tenant change-role` — these require Admin auth policy (not ReadOnly) and can be added as a follow-up story.
- **`--status` filter on `tenant list`** — filter by Active/Suspended/Onboarding.
- **`--watch` mode on `tenant verify`** — continuous monitoring with polling interval.
- **`tenant verify` with `--strict`** — treat WARNING as FAIL for CI/CD gates that require binary pass/fail.

### Code style — follow project conventions exactly

- File-scoped namespaces: `namespace Hexalith.EventStore.Admin.Cli.Commands.Tenant;`
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

JSON output uses `JsonNamingPolicy.CamelCase` via `JsonDefaults.Options`. Property names: `tenantId`, `displayName`, `status`, `eventCount`, `domainCount`, `storageBytes`, `createdAtUtc`, `subscriptionTier`, `maxEventsPerDay`, `maxStorageBytes`, `currentUsage`. Operators write `jq` filters like `jq '.[] | select(.status == "active")'`.

### CI/CD usage examples

```bash
# List all tenants
eventstore-admin tenant list --format json | jq '.[] | .tenantId'

# Check tenant details
eventstore-admin tenant detail acme-corp --format json

# Monitor tenant quotas (exit code 1 = approaching limit)
eventstore-admin tenant quotas acme-corp --quiet

# Compare usage across tenants
eventstore-admin tenant compare acme-corp beta-inc gamma-llc --format csv

# CI/CD gate — verify tenant compliance
eventstore-admin tenant verify acme-corp --quiet
echo $?  # 0=PASS, 1=WARNING, 2=FAIL

# GitHub Actions step
- name: Verify tenant compliance
  run: eventstore-admin tenant verify ${{ vars.TENANT_ID }} --quiet --url ${{ secrets.ADMIN_URL }}

# Write verification report to file silently
eventstore-admin tenant verify acme-corp --quiet --output /tmp/tenant-verify.json --format json

# Cron monitoring — log tenant status
eventstore-admin tenant list --format csv >> /var/log/eventstore-tenants.csv
```

### Folder structure

```
src/Hexalith.EventStore.Admin.Cli/
  Commands/
    Tenant/
      TenantCommand.cs               <-- NEW (parent command)
      TenantArguments.cs             <-- NEW (shared positional argument)
      TenantListCommand.cs           <-- NEW
      TenantDetailCommand.cs         <-- NEW
      TenantQuotasCommand.cs         <-- NEW
      TenantUsersCommand.cs          <-- NEW
      TenantCompareCommand.cs        <-- NEW
      TenantVerifyCommand.cs         <-- NEW

tests/Hexalith.EventStore.Admin.Cli.Tests/
  Commands/
    Tenant/
      TenantListCommandTests.cs      <-- NEW
      TenantDetailCommandTests.cs    <-- NEW
      TenantQuotasCommandTests.cs    <-- NEW
      TenantUsersCommandTests.cs     <-- NEW
      TenantCompareCommandTests.cs   <-- NEW
      TenantVerifyCommandTests.cs    <-- NEW
```

### Modified files

- `src/Hexalith.EventStore.Admin.Cli/Program.cs` — replace tenant stub with `TenantCommand.Create(binding)`
- No changes to `AdminApiClient`, `ExitCodes`, formatting infrastructure, or any other existing files

### Git commit patterns from recent work

Recent commits: `feat: <description> for story <story-id>`
Branch naming: `feat/story-17-5-tenant-subcommand-list-quotas-verify`

### Previous story intelligence (17-1, 17-2, 17-3)

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

Follow these as canonical patterns.

### Architecture: CLI is a thin HTTP client (ADR-P4)

Per ADR-P4, the CLI never accesses DAPR directly. All tenant operations go through `AdminApiClient` -> Admin REST API -> DAPR -> Hexalith.Tenants. No DAPR SDK, no Aspire dependencies.

### References

- [Source: _bmad-output/planning-artifacts/prd.md — FR77: tenant management, quotas, isolation verification]
- [Source: _bmad-output/planning-artifacts/prd.md — FR79: three-interface shared Admin API]
- [Source: _bmad-output/planning-artifacts/prd.md — FR80: CLI output formats, exit codes]
- [Source: _bmad-output/planning-artifacts/prd.md — NFR42: CLI startup + query within 3 seconds]
- [Source: _bmad-output/planning-artifacts/architecture.md — ADR-P4: CLI is thin HTTP client, no DAPR]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR50: tenant subcommand in CLI tree]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR52: exit codes 0/1/2 for CI/CD]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-03-21-tenant-management.md — tenant operations delegated to Hexalith.Tenants]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/ — all tenant DTOs]
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs — REST API endpoints]
- [Source: src/Hexalith.EventStore.Admin.Cli/Commands/Projection/ — projection subcommand (pattern reference)]
- [Source: src/Hexalith.EventStore.Admin.Cli/Commands/Stream/ — stream subcommand (pattern reference)]
- [Source: src/Hexalith.EventStore.Admin.Cli/ExitCodes.cs — exit code constants]
- [Source: _bmad-output/implementation-artifacts/17-3-projection-subcommand-list-status-pause-resume-reset.md — projection story (pattern reference)]
- [Source: _bmad-output/implementation-artifacts/17-4-health-subcommand-exit-codes-for-cicd.md — health story (quiet mode pattern reference)]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Fixed `Argument<T>` constructor — System.CommandLine requires property initializer for Description, not constructor parameter.
- Fixed CA2007 ConfigureAwait warning in TenantCompareCommandTests.

### Completion Notes List

- All 5 tasks (8 source files, 6 test files, 1 modified file) implemented and tested.
- 30 new unit tests added across 6 test files — all passing.
- All 147 CLI tests pass with 0 failures and 0 warnings.
- All Tier 1 tests (Contracts: 271, Client: 297, Sample: 62, Testing: 67, SignalR: 27, CLI: 147) pass with 0 regressions.
- Build succeeds with 0 warnings in Release configuration.
- Six tenant sub-subcommands implemented: list, detail, quotas, users, compare, verify.
- Replaced tenant stub command in Program.cs with TenantCommand parent command.
- Follows established patterns from stories 17-1 through 17-4.

### File List

**New files:**
- src/Hexalith.EventStore.Admin.Cli/Commands/Tenant/TenantArguments.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Tenant/TenantCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Tenant/TenantListCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Tenant/TenantDetailCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Tenant/TenantQuotasCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Tenant/TenantUsersCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Tenant/TenantCompareCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/Tenant/TenantVerifyCommand.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Tenant/TenantListCommandTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Tenant/TenantDetailCommandTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Tenant/TenantQuotasCommandTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Tenant/TenantUsersCommandTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Tenant/TenantCompareCommandTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Tenant/TenantVerifyCommandTests.cs

**Modified files:**
- src/Hexalith.EventStore.Admin.Cli/Program.cs

### Change Log

- 2026-03-25: Story 17-5 implemented — tenant subcommand with 6 sub-subcommands (list, detail, quotas, users, compare, verify), 30 unit tests.
