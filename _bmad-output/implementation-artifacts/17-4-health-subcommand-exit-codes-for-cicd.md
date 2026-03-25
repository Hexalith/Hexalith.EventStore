# Story 17.4: Health Subcommand — Exit Codes for CI/CD

Status: done

Size: Small-Medium — ~4 new/modified files, 4 task groups, 9 ACs, ~19 tests (~5-7 hours estimated). Enhances the existing `health` command (from story 17-1) with CI/CD-specific options (`--strict`, `--wait`, `--timeout`, `--quiet`) and adds a `health dapr` sub-subcommand for component-level health checks. Uses the existing `GET /api/v1/admin/health/dapr` endpoint not yet consumed by the CLI.

**Dependency:** Story 17-1 must be complete (done). This story modifies `HealthCommand.cs` and adds a sub-subcommand. Stories 17-2 and 17-3 are independent.

## Definition of Done

- All 9 ACs verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- `eventstore-admin health --strict` returns exit code 2 for degraded systems
- `eventstore-admin health --wait --timeout 30` polls until healthy or times out
- `eventstore-admin health --quiet` suppresses stdout, only returns exit code
- `eventstore-admin health dapr` lists DAPR component statuses
- `eventstore-admin health dapr --component state-redis` checks a single component

## Story

As a **CI/CD pipeline engineer or platform operator**,
I want **the `eventstore-admin health` command to support strict pass/fail exit codes, polling with timeout, quiet mode, and per-component DAPR health checks**,
so that **I can use the CLI as a deployment gate, startup probe, and monitoring script without parsing text output (FR80, UX-DR52, NFR42)**.

## Acceptance Criteria

1. **`--strict` option** — `eventstore-admin health --strict` treats degraded status as error: exit code `2` instead of `1`. Without `--strict`, behavior is unchanged (Healthy=0, Degraded=1, Unhealthy=2). `--strict` is a boolean option (`Option<bool>`, default `false`). This enables CI/CD gates that only understand 0/non-zero. The option is local to the `health` command (not recursive/global).

2. **`--wait` option** — `eventstore-admin health --wait` polls the health endpoint repeatedly until the status is acceptable. **Without `--strict`:** both `Healthy` and `Degraded` are acceptable (poll stops, exit code 0). **With `--strict`:** only `Healthy` is acceptable (poll continues on `Degraded`). Polling interval: configurable internally (default 1 second in production, injectable for tests). Between polls, prints "Waiting for healthy status... (attempt {n})" to stderr. When acceptable status is achieved: prints "Service is healthy." to stderr, then renders the final health report to stdout per `--format`. When timeout expires: prints "Timed out waiting for healthy status after {t} seconds." to stderr, exit code `2`. Connection errors during polling are not fatal — the command retries until timeout. `--wait` is a boolean option (`Option<bool>`, default `false`).

3. **`--timeout` option** — `eventstore-admin health --wait --timeout 60` sets the maximum wait time in seconds. Default: `30`. `--timeout` without `--wait` is ignored (no error, just unused). `--timeout` is an `Option<int>` with default `30`. Minimum value: `1`. Use `AddValidator` to reject values less than 1 with a custom error message ("Timeout must be at least 1 second.").

4. **`--quiet` / `-q` option** — `eventstore-admin health --quiet` suppresses all stdout output. Only the exit code is meaningful. Errors still go to stderr. `--wait` progress messages still go to stderr. `--quiet` is a boolean option (`Option<bool>`, alias `-q`, default `false`). When `--quiet` is active and `--output` is NOT specified, skip `OutputWriter.Write()` entirely — do not create or format the output string. **However:** `--quiet` with `--output <file>` still writes to the file (stdout is suppressed but file output is preserved). This supports the CI/CD pattern: `health --wait --quiet --output /tmp/health.json` waits silently then writes the final report to a file for downstream parsing.

5. **`health dapr` sub-subcommand** — `eventstore-admin health dapr` calls `GET /api/v1/admin/health/dapr` and displays a table of `DaprComponentHealth` records. Columns: Component Name (ComponentName), Type (ComponentType), Status (Status), Last Check (LastCheckUtc). Exit code: `0` if all components are `Healthy`, `1` if any are `Degraded` (none `Unhealthy`), `2` if any are `Unhealthy` or if the list is empty. JSON format: serialize the entire `List<DaprComponentHealth>`. CSV format: same columns as table. Empty result prints "No DAPR components found." to stderr, exit code `2`.

6. **`--component <name>` filter on `health dapr`** — `eventstore-admin health dapr --component state-redis` filters the response to the named component. If found: displays that component's health and exits with the corresponding exit code (Healthy=0, Degraded=1, Unhealthy=2). If not found: prints "DAPR component 'state-redis' not found." to stderr, exit code `2`. `--component` is an `Option<string?>` (alias `-c`, default `null`). When null, all components are shown.

7. **`health` becomes a parent command** — Convert `health` from a leaf command to a parent command with sub-subcommand `dapr`. Running `eventstore-admin health` (no subcommand) still executes the existing system health check (this is the default behavior via `SetAction` on the parent command). Running `eventstore-admin health dapr` executes the DAPR component check. `System.CommandLine` supports this: a parent command can have both `SetAction` (default handler) and subcommands. **Note:** This is a new pattern in this CLI — existing parent commands (`stream`, `projection`) have no default handler. This is the first command combining `SetAction` with subcommands.

8. **Option interaction** — `--strict` and `--quiet` work independently and compose: `eventstore-admin health --strict --quiet` returns 0 only if healthy, suppresses all output. `--wait` composes with both: `eventstore-admin health --wait --strict --quiet` polls silently, exits 0 only on healthy. `--strict`, `--quiet`, `--wait`, `--timeout` are only on the parent `health` command — they do NOT apply to `health dapr`. The `health dapr` sub-subcommand only has `--component`.

9. **Test coverage** — Unit tests cover: `--strict` exit code mapping (degraded→2, healthy→0, unhealthy→2), `--quiet` output suppression, `--wait` polling loop (mock successive unhealthy then healthy responses), `--wait` timeout scenario, `--wait` with cancellation token, `health dapr` table/JSON/CSV output, `--component` filter (found, not found, case-insensitive), empty DAPR component list, exit code derivation from component statuses, and `--wait` with connection errors during polling. **Important:** The poll interval must be injectable (internal parameter) so tests use 1ms intervals instead of 1-second production delays.

## Tasks / Subtasks

- [x] **Task 1: Add CI/CD options to `HealthCommand` and refactor to parent command** (AC: 1, 2, 3, 4, 7, 8)
  - [x] 1.1 Modify `HealthCommand.Create(GlobalOptionsBinding)`:
    - Add four local options: `--strict` (`Option<bool>`, default false), `--wait` (`Option<bool>`, default false), `--timeout` (`Option<int>`, default 30, `AddValidator` for min value 1), `--quiet` / `-q` (`Option<bool>`, default false).
    - Add `HealthDaprCommand.Create(binding)` as a subcommand.
    - Keep `SetAction` on the parent command — `System.CommandLine` routes to the handler when no subcommand is specified.
    - Resolve new options from `parseResult` alongside existing `GlobalOptions`:
    ```csharp
    bool strict = parseResult.GetValue(strictOption);
    bool wait = parseResult.GetValue(waitOption);
    int timeout = parseResult.GetValue(timeoutOption);
    bool quiet = parseResult.GetValue(quietOption);
    ```
  - [x] 1.2 Refactor `ExecuteAsync` signature to accept new parameters including injectable poll interval:
    ```csharp
    internal static async Task<int> ExecuteAsync(
        GlobalOptions options,
        bool strict,
        bool wait,
        int timeout,
        bool quiet,
        CancellationToken cancellationToken,
        int pollIntervalMs = 1000)
    ```
    The `pollIntervalMs` parameter defaults to 2000 (production). Tests pass `1` for instant polling. This prevents 2-second delays per test iteration.
  - [x] 1.3 Implement `--strict` logic — modify the exit code mapping:
    ```csharp
    int MapExitCode(HealthStatus status, bool strict) => status switch
    {
        HealthStatus.Healthy => ExitCodes.Success,
        HealthStatus.Degraded => strict ? ExitCodes.Error : ExitCodes.Degraded,
        _ => ExitCodes.Error,
    };
    ```
  - [x] 1.4 Implement `--wait` polling loop:
    ```csharp
    if (wait)
    {
        Stopwatch sw = Stopwatch.StartNew();
        int attempt = 0;
        while (sw.Elapsed.TotalSeconds < timeout)
        {
            attempt++;
            try
            {
                SystemHealthReport report = await client
                    .GetAsync<SystemHealthReport>("/api/v1/admin/health", cancellationToken)
                    .ConfigureAwait(false);
                int exitCode = MapExitCode(report.OverallStatus, strict);
                if (exitCode == ExitCodes.Success)
                {
                    Console.Error.WriteLine("Service is healthy.");
                    // Render final report unless quiet
                    if (!quiet) { /* format and write output */ }
                    return ExitCodes.Success;
                }
            }
            catch (AdminApiException)
            {
                // Connection errors during polling are not fatal — retry
            }
            Console.Error.WriteLine($"Waiting for healthy status... (attempt {attempt})");
            await Task.Delay(pollIntervalMs, cancellationToken).ConfigureAwait(false);
        }
        Console.Error.WriteLine($"Timed out waiting for healthy status after {timeout} seconds.");
        return ExitCodes.Error;
    }
    ```
    **Wait + strict interaction:** Without `--strict`, both `Healthy` and `Degraded` are "acceptable" (exit code 0 from `MapExitCode`) — the poll continues only for `Unhealthy`. With `--strict`, only `Healthy` produces exit code 0 — `Degraded` maps to exit code 2 and the poll continues. The `MapExitCode` function naturally handles this: exit code `0` means "acceptable, stop polling".
  - [x] 1.5 Implement `--quiet` — when quiet is true and `options.OutputFile` is null, skip output formatting and `OutputWriter.Write()` entirely. When quiet is true but `options.OutputFile` is specified, still format and write to the file (suppress stdout only, not file output). Still execute the health check and return the correct exit code in both cases.

- [x] **Task 2: `health dapr` sub-subcommand** (AC: 5, 6)
  - [x] 2.1 Create `Commands/HealthDaprCommand.cs`:
    - One optional option: `--component` / `-c` (`Option<string?>`, default null).
    - Handler: resolves global options, creates `AdminApiClient`, calls `GET /api/v1/admin/health/dapr`.
    - Deserializes as `List<DaprComponentHealth>` (concrete type).
    - When `--component` is provided: filter the list with `components.FirstOrDefault(c => string.Equals(c.ComponentName, component, StringComparison.OrdinalIgnoreCase))`. If null: print "DAPR component '{component}' not found." to stderr, exit code `2`. If found: display single component (key/value for table, object for JSON/CSV single-row).
    - When `--component` is not provided: display full list.
    - Exit code derivation:
    ```csharp
    static int DeriveExitCode(IReadOnlyList<DaprComponentHealth> components)
    {
        if (components.Count == 0) return ExitCodes.Error;
        if (components.Any(c => c.Status == HealthStatus.Unhealthy)) return ExitCodes.Error;
        if (components.Any(c => c.Status == HealthStatus.Degraded)) return ExitCodes.Degraded;
        return ExitCodes.Success;
    }
    ```
    - Column definitions for table/CSV:
    ```csharp
    List<ColumnDefinition> columns =
    [
        new("Component Name", "ComponentName"),
        new("Type", "ComponentType"),
        new("Status", "Status"),
        new("Last Check", "LastCheckUtc"),
    ];
    ```
    These are identical to the component columns in `HealthCommand` — reuse the same column definitions. Consider extracting a `HealthColumns.DaprComponents()` static method if the duplication bothers you, but inlining is fine for two usages.
  - [x] 2.2 Register in `HealthCommand.Create`: add `command.Subcommands.Add(HealthDaprCommand.Create(binding));`

- [x] **Task 3: Add testable overloads** (AC: 9)
  - [x] 3.1 Add `internal static` overload to `HealthCommand.ExecuteAsync` that accepts `AdminApiClient` and injectable poll interval:
    ```csharp
    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        bool strict,
        bool wait,
        int timeout,
        bool quiet,
        CancellationToken cancellationToken,
        int pollIntervalMs = 1000)
    ```
    The public `ExecuteAsync` creates the client and delegates. This enables unit tests to inject `MockHttpMessageHandler` and use `pollIntervalMs: 1` for instant polling.
  - [x] 3.2 Add `internal static` overload to `HealthDaprCommand.ExecuteAsync` that accepts `AdminApiClient`:
    ```csharp
    internal static async Task<int> ExecuteAsync(
        AdminApiClient client,
        GlobalOptions options,
        string? component,
        CancellationToken cancellationToken)
    ```

- [x] **Task 4: Unit tests** (AC: 9)
  - [x] **Health strict mode tests:**
  - [x] 4.1 `HealthCommand_Strict_DegradedReturnsExitCode2` — With `--strict`, degraded health returns exit code `2` (not `1`).
  - [x] 4.2 `HealthCommand_Strict_HealthyReturnsExitCode0` — With `--strict`, healthy still returns `0`.
  - [x] 4.3 `HealthCommand_NoStrict_DegradedReturnsExitCode1` — Without `--strict`, degraded returns `1` (existing behavior preserved).
  - [x] **Health quiet mode tests:**
  - [x] 4.4 `HealthCommand_Quiet_SuppressesStdout` — With `--quiet`, stdout is empty. Verify by capturing `Console.Out` via `StringWriter`. Exit code matches health status.
  - [x] 4.5 `HealthCommand_Quiet_StillReturnsCorrectExitCode` — With `--quiet`, exit code `0` for healthy, `2` for unhealthy.
  - [x] **Health wait mode tests:**
  - [x] 4.6 `HealthCommand_Wait_PollsUntilHealthy` — Mock handler returns Unhealthy on first 2 calls, Healthy on 3rd. Verify exit code `0` and "Service is healthy." printed to stderr. Use `MockHttpMessageHandler` with a response queue (create a `QueuedMockHttpMessageHandler` that returns different responses on successive calls).
  - [x] 4.7 `HealthCommand_Wait_Timeout_ReturnsError` — Mock handler always returns Unhealthy. Set `--timeout 3`. Verify exit code `2` and "Timed out" message to stderr. Use a very short timeout.
  - [x] 4.8 `HealthCommand_Wait_ConnectionError_Retries` — Mock handler throws `HttpRequestException` on first call, returns Healthy on second. Verify exit code `0` — connection errors during polling are not fatal.
  - [x] 4.9 `HealthCommand_Wait_Strict_OnlyAcceptsHealthy` — With `--wait --strict`, degraded status continues polling (not considered acceptable). Mock: Degraded, Degraded, Healthy → exit code `0`.
  - [x] 4.10 `HealthCommand_Wait_CancellationToken_StopsPolling` — Cancel via `CancellationTokenSource` during `--wait` polling. Verify `TaskCanceledException` or `OperationCanceledException` propagates cleanly (no hang, no swallowed cancellation).
  - [x] **Health dapr tests:**
  - [x] 4.11 `HealthDaprCommand_ReturnsComponentTable` — Mocked response with 3 components renders table with correct columns.
  - [x] 4.12 `HealthDaprCommand_JsonFormat_ReturnsValidJson` — JSON output deserializes back to `List<DaprComponentHealth>`.
  - [x] 4.13 `HealthDaprCommand_EmptyResult_PrintsError` — Empty list prints "No DAPR components found." to stderr, exit code `2`.
  - [x] 4.14 `HealthDaprCommand_ExitCode_AllHealthy_Returns0` — All components Healthy → exit code `0`.
  - [x] 4.15 `HealthDaprCommand_ExitCode_AnyDegraded_Returns1` — Mix of Healthy+Degraded → exit code `1`.
  - [x] 4.16 `HealthDaprCommand_ExitCode_AnyUnhealthy_Returns2` — Any Unhealthy component → exit code `2`.
  - [x] **Health dapr --component tests:**
  - [x] 4.17 `HealthDaprCommand_ComponentFilter_Found_ReturnsComponent` — `--component state-redis` with matching component → displays single component, exit code matches its status.
  - [x] 4.18 `HealthDaprCommand_ComponentFilter_NotFound_ReturnsError` — `--component nonexistent` → prints "DAPR component 'nonexistent' not found." to stderr, exit code `2`.
  - [x] 4.19 `HealthDaprCommand_ComponentFilter_CaseInsensitive` — `--component STATE-REDIS` matches `state-redis`. Case-insensitive matching.

  Test file locations:
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/HealthCommandStrictTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/HealthCommandWaitTests.cs`
  - `tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/HealthDaprCommandTests.cs`

  **Test pattern:** Reuse `MockHttpMessageHandler` from the test project. For `--wait` tests, create a `QueuedMockHttpMessageHandler` that returns responses from a queue on successive calls. Construct `AdminApiClient` with `internal AdminApiClient(HttpClient)` constructor. Capture stderr via `Console.SetError(new StringWriter())` for message assertions.

## Dev Notes

### Existing test compatibility — do NOT break

The existing `HealthCommandTests.cs` has 6 passing tests that call `ExecuteHealthWithMockAsync` using the current 2-parameter `ExecuteAsync(GlobalOptions, CancellationToken)` signature. When refactoring to the new multi-parameter signature, update these tests to pass default values (`strict: false, wait: false, timeout: 30, quiet: false`). All 6 existing tests must continue to pass unchanged in behavior.

### Builds on story 17-1 infrastructure — do NOT recreate

All of the following already exist from story 17-1. Reuse them:
- `GlobalOptions` / `GlobalOptionsBinding` — global option parsing and resolution
- `AdminApiClient` — HTTP client with auth, error handling, shared `JsonSerializerOptions`. **No changes needed to AdminApiClient for this story.**
- `IOutputFormatter` / `OutputFormatterFactory` / `OutputWriter` — formatting infrastructure
- `ColumnDefinition` / `Alignment` — column metadata for table/CSV
- `ExitCodes` — exit code constants (Success=0, Degraded=1, Error=2)
- `JsonDefaults.Options` — shared `JsonSerializerOptions` with `JsonStringEnumConverter` (camelCase naming)
- `MockHttpMessageHandler` — test helper for HTTP mocking
- `HealthCommand` — existing health command (this story modifies it)

### Admin API endpoints used

Both endpoints already exist in `src/Hexalith.EventStore.Admin.Server/Controllers/AdminHealthController.cs`:

| Method | Route | Auth | Returns | HTTP Status |
|--------|-------|------|---------|-------------|
| GET | `api/v1/admin/health` | ReadOnly | `SystemHealthReport` | 200 / 503 |
| GET | `api/v1/admin/health/dapr` | ReadOnly | `IReadOnlyList<DaprComponentHealth>` | 200 / 503 |

### Existing Admin.Abstractions models — do NOT recreate

All DTOs are defined in `src/Hexalith.EventStore.Admin.Abstractions/Models/Health/`:

- `SystemHealthReport(OverallStatus, TotalEventCount, EventsPerSecond, ErrorPercentage, DaprComponents, ObservabilityLinks)` — system health report
- `HealthStatus` enum: `Healthy`, `Degraded`, `Unhealthy`
- `DaprComponentHealth(ComponentName, ComponentType, Status, LastCheckUtc)` — per-component health
- `ObservabilityLinks(TraceUrl, MetricsUrl, LogsUrl)` — dashboard deep links

### Key difference from story 17-1: CI/CD-specific enhancements

Story 17-1 created the basic `health` command with exit codes. This story adds:

1. **`--strict`** — Binary pass/fail for CI/CD gates. Many CI/CD tools only check exit code 0 vs non-zero. The current exit code 1 (degraded) may be treated as failure by some tools (Jenkins, GitHub Actions) but as success by others. `--strict` makes it explicit.

2. **`--wait` + `--timeout`** — Startup probe pattern. After deploying a service, CI/CD pipelines need to wait for it to become healthy before proceeding. Without `--wait`, the first health check after deploy fails because the service hasn't started. Common usage:
   ```bash
   # In a deployment script:
   eventstore-admin health --wait --timeout 120 --strict --quiet
   ```

3. **`--quiet`** — Keeps CI/CD logs clean. In automated pipelines, the human-readable output is noise. Only the exit code matters.

4. **`health dapr`** — Component-level checks for targeted monitoring. A pipeline might only care about a specific DAPR component (e.g., the state store). The existing `/api/v1/admin/health/dapr` endpoint is already implemented but not consumed by the CLI.

### System.CommandLine: parent command with both handler and subcommands

`System.CommandLine` supports this pattern — a parent command can have both `SetAction` (default handler) and subcommands:

```csharp
Command command = new("health", "Show system health status");
// This handler runs when no subcommand is given:
command.SetAction(async (parseResult, cancellationToken) => { ... });
// This subcommand runs when "health dapr" is given:
command.Subcommands.Add(HealthDaprCommand.Create(binding));
```

When the user runs `eventstore-admin health`, `SetAction` executes. When they run `eventstore-admin health dapr`, the subcommand's handler executes. Options defined on the parent (`--strict`, etc.) are not inherited by the subcommand unless marked `Recursive = true` — keep them non-recursive (local to `health`).

### Wait loop implementation notes

- Use `System.Diagnostics.Stopwatch` for elapsed time tracking (not `DateTime.Now`).
- The poll interval uses `Task.Delay(pollIntervalMs, cancellationToken)`. Production default: 1000ms. Tests pass `pollIntervalMs: 1` to avoid real delays. If the user cancels (Ctrl+C), the `CancellationToken` breaks the delay immediately — do NOT catch `OperationCanceledException` in the wait loop (let it propagate for clean cancellation).
- **ALL `AdminApiException` types are retried** during `--wait`: this includes HTTP 503 (service unavailable), HTTP 500 (server error), connection refused (`SocketException`), and HTTP timeouts. The `AdminApiClient.GetAsync` already wraps all of these as `AdminApiException`. The wait loop catches `AdminApiException` generically — do NOT catch specific subtypes.
- **HTTP timeout interaction:** `AdminApiClient` has a 10-second HTTP timeout. When the server is completely down, each `GetAsync` call takes ~10 seconds before throwing. The effective poll cadence in worst case is `pollIntervalMs + 10s HTTP timeout`. The `Stopwatch` wall-clock tracking handles this correctly — do NOT modify `AdminApiClient.Timeout` for wait mode.
- The final successful health report (when `--wait` succeeds) is rendered to stdout unless `--quiet` is set (or to file if `--output` is specified).

### QueuedMockHttpMessageHandler for wait tests

The existing `MockHttpMessageHandler` returns the same response every time. For `--wait` tests, create a test helper that returns responses from a queue:

```csharp
internal class QueuedMockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responseFactories;

    public QueuedMockHttpMessageHandler(params Func<HttpResponseMessage>[] factories)
    {
        _responseFactories = new Queue<Func<HttpResponseMessage>>(factories);
    }

    /// <summary>Convenience: queue plain responses (no exceptions).</summary>
    public QueuedMockHttpMessageHandler(params HttpResponseMessage[] responses)
        : this(responses.Select<HttpResponseMessage, Func<HttpResponseMessage>>(r => () => r).ToArray())
    {
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_responseFactories.Count == 0)
            throw new InvalidOperationException("No more queued responses.");
        return Task.FromResult(_responseFactories.Dequeue()());
    }
}
```

Supports both normal responses and exception-throwing entries via lambda factories. Usage for connection error test:
```csharp
var handler = new QueuedMockHttpMessageHandler(
    new Func<HttpResponseMessage>(() => throw new HttpRequestException("Connection refused")),
    () => healthyResponse);
```

Place this in `tests/Hexalith.EventStore.Admin.Cli.Tests/Client/QueuedMockHttpMessageHandler.cs`.

### Folder structure

```
src/Hexalith.EventStore.Admin.Cli/
  Commands/
    HealthCommand.cs                         <-- MODIFIED (add options + sub-subcommand)
    HealthDaprCommand.cs                     <-- NEW

tests/Hexalith.EventStore.Admin.Cli.Tests/
  Client/
    QueuedMockHttpMessageHandler.cs          <-- NEW (test helper)
  Commands/
    HealthCommandStrictTests.cs              <-- NEW
    HealthCommandWaitTests.cs                <-- NEW
    HealthDaprCommandTests.cs                <-- NEW
```

### Modified files

- `src/Hexalith.EventStore.Admin.Cli/Commands/HealthCommand.cs` — add `--strict`, `--wait`, `--timeout`, `--quiet` options; add `HealthDaprCommand` as subcommand; refactor `ExecuteAsync` signature
- No changes to `Program.cs` — `HealthCommand.Create(binding)` is already registered

### Architecture: CLI is a thin HTTP client (ADR-P4)

Per ADR-P4, the CLI never accesses DAPR directly. Health checks go through `AdminApiClient` -> Admin REST API -> DAPR. No DAPR SDK, no Aspire dependencies.

### Code style — follow project conventions exactly

- File-scoped namespaces: `namespace Hexalith.EventStore.Admin.Cli.Commands;`
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
    bool strict = parseResult.GetValue(strictOption);
    // ...
    return await ExecuteAsync(options, strict, wait, timeout, quiet, cancellationToken)
        .ConfigureAwait(false);
});
```

### JSON output uses camelCase — critical for scriptability

JSON output uses `JsonNamingPolicy.CamelCase` via `JsonDefaults.Options`. Property names: `componentName`, `componentType`, `status`, `lastCheckUtc`, `overallStatus`, `totalEventCount`, `eventsPerSecond`, `errorPercentage`, `daprComponents`, `observabilityLinks`. Operators write `jq` filters like `jq '.[] | select(.status == "healthy")'`.

### CI/CD usage examples

```bash
# Kubernetes readiness probe
eventstore-admin health --quiet --strict --url $ADMIN_URL --token $TOKEN

# Deployment gate — wait for service startup
eventstore-admin health --wait --timeout 120 --strict --quiet

# Wait silently, then write final report to file for downstream parsing
eventstore-admin health --wait --timeout 60 --quiet --output /tmp/health.json --format json

# Check specific DAPR component in monitoring script
eventstore-admin health dapr --component state-redis --format json

# GitHub Actions step
- name: Health gate
  run: eventstore-admin health --strict --quiet --url ${{ secrets.ADMIN_URL }}

# Cron monitoring — log component status
eventstore-admin health dapr --format csv >> /var/log/eventstore-health.csv
```

**Important for `set -e` scripts:** Exit code 1 (degraded) triggers `set -e` failure. Always use `--strict` in CI/CD scripts, or handle exit code 1 explicitly:
```bash
set -e
# Option A: --strict makes it binary (0 or 2)
eventstore-admin health --strict --quiet
# Option B: handle degraded explicitly
eventstore-admin health --quiet || [ $? -eq 1 ] && echo "Degraded but acceptable"
```

### Git commit patterns from recent work

Recent commits: `feat: <description> for story <story-id>`
Branch naming: `feat/story-17-4-health-subcommand-exit-codes-for-cicd`

### Previous story intelligence (17-1 and 17-3)

Story 17-1 established:
- `System.CommandLine` package version in `Directory.Packages.props`
- Root command structure in `Program.cs` with `GlobalOptionsBinding` pattern
- `AdminApiClient` with `GetAsync<T>`, `TryGetAsync<T>`, and `PostAsync<TResponse>` methods
- Output formatting infrastructure
- `ExitCodes` static class (Success=0, Degraded=1, Error=2)
- `HealthCommand` as the working reference for dual-section output
- `MockHttpMessageHandler` test helper
- Handler wiring: `command.SetAction(async (parseResult, cancellationToken) => { ... })`

Story 17-3 established:
- Parent command with sub-subcommands pattern (ProjectionCommand)
- `--component` filter pattern is similar to `--tenant` filter on projection list

Follow these as canonical patterns.

### References

- [Source: _bmad-output/planning-artifacts/prd.md — FR80: JSON/CSV/table output, exit codes, shell completions]
- [Source: _bmad-output/planning-artifacts/prd.md — NFR42: CLI startup + query within 3 seconds]
- [Source: _bmad-output/planning-artifacts/architecture.md — ADR-P4: CLI is thin HTTP client, no DAPR]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR52: exit codes 0/1/2 for CI/CD gates]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Health/ — all health DTOs]
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/AdminHealthController.cs — REST API endpoints including /dapr]
- [Source: src/Hexalith.EventStore.Admin.Cli/Commands/HealthCommand.cs — existing health command to modify]
- [Source: src/Hexalith.EventStore.Admin.Cli/ExitCodes.cs — exit code constants]
- [Source: _bmad-output/implementation-artifacts/17-3-projection-subcommand-list-status-pause-resume-reset.md — projection subcommand story (pattern reference)]

### Future enhancements (out of scope)

- `--component` accepting comma-separated list (e.g., `--component state-redis,pubsub-redis`) — enables checking multiple components in a single invocation. For now, operators can script multiple calls.
- `--strict` on `health dapr` — collapse exit code 1→2 for component checks. Currently, most CI/CD tools treat exit code 1 as failure anyway, so this is low priority.

### Project Structure Notes

- `HealthDaprCommand.cs` sits in `Commands/` alongside `HealthCommand.cs` (not in a subfolder) since there's only one sub-subcommand
- `QueuedMockHttpMessageHandler.cs` sits alongside the existing `MockHttpMessageHandler.cs` in `Client/`
- No new projects or packages needed — all dependencies already exist
- No changes to `Program.cs` — the health command is already registered

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- System.CommandLine beta5 API uses `Validators.Add()` instead of `AddValidator()` — fixed at build time
- xUnit analyzer xUnit1030 forbids `.ConfigureAwait(false)` in test methods — removed from all test methods
- QueuedMockHttpMessageHandler initially threw `InvalidOperationException` when queue exhausted during polling tests — added repeat-last behavior to replay the last factory when queue is empty
- CA2007 conflict: inner `await` in `Should.ThrowAsync` lambda triggers CA2007 — fixed by removing inner async/await (pass task directly)

### Completion Notes List

- Modified `HealthCommand.cs` to add `--strict`, `--wait`, `--timeout`, `--quiet` options with CI/CD polling loop, strict exit code mapping, and quiet output suppression
- `health` command now serves as parent command with `SetAction` (default handler) and `dapr` sub-subcommand — first command in this CLI combining both patterns
- Created `HealthDaprCommand.cs` with `--component` / `-c` filter, case-insensitive matching, table/JSON/CSV output, and component-based exit code derivation
- Both commands have `internal static` testable overloads accepting `AdminApiClient` directly
- `HealthCommand.ExecuteAsync` accepts injectable `pollIntervalMs` (default 1000ms, tests use 1ms)
- Created `QueuedMockHttpMessageHandler` test helper with repeat-last behavior for polling tests
- 19 new tests: 3 strict, 2 quiet, 5 wait (polling, timeout, connection error, strict interaction, cancellation), 6 dapr (table, JSON, empty, exit codes), 3 component filter (found, not found, case-insensitive)
- All 117 CLI tests pass (98 existing + 19 new), zero regressions
- All 841 Tier 1 tests pass across 6 test projects
- Build passes with zero warnings in Release configuration

### Change Log

- 2026-03-25: Implemented Story 17-4 — Health subcommand CI/CD options (--strict, --wait, --timeout, --quiet) and health dapr sub-subcommand with --component filter

### File List

New files:
- src/Hexalith.EventStore.Admin.Cli/Commands/HealthDaprCommand.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Client/QueuedMockHttpMessageHandler.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/HealthCommandStrictTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/HealthCommandWaitTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/HealthDaprCommandTests.cs

Modified files:
- src/Hexalith.EventStore.Admin.Cli/Commands/HealthCommand.cs
- _bmad-output/implementation-artifacts/sprint-status.yaml
- _bmad-output/implementation-artifacts/17-4-health-subcommand-exit-codes-for-cicd.md
