# Story 17.1: CLI Scaffold, System.CommandLine & Global Options

Status: done

Size: Large — ~15 new files, 6 task groups, 12 ACs, ~28 tests (~10-14 hours estimated). Greenfield project scaffold: new `Admin.Cli` console app with `System.CommandLine` root command, global options (--url, --token, --format, --output), output formatting infrastructure (JSON/CSV/table), exit code conventions, Admin API HTTP client base, and a working `health` subcommand as proof of concept.

**Split advisory (recommended):** Deliver in two PRs. (A) Tasks 1-3 (project scaffold + root command + global options + output formatting infrastructure) — independently testable with no server dependency. (B) Tasks 4-6 (Admin API client + health command + stubs + integration tests). PR A delivers the CLI skeleton that all future subcommands build on; PR B adds the first working command. The natural split is "CLI framework" vs "first API integration."

## Definition of Done

- All 12 ACs verified
- All unit tests green (Task 5)
- Project builds with zero warnings in CI (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- `dotnet run --project src/Hexalith.EventStore.Admin.Cli -- health` returns formatted output with correct exit code
- .NET global tool packaging works: `dotnet pack` produces `Hexalith.EventStore.Admin.Cli` nupkg

## Story

As a **platform operator or CI/CD pipeline engineer**,
I want **a .NET global tool (`eventstore-admin`) with System.CommandLine-based argument parsing, global options for server URL, authentication token, and output format (JSON/CSV/table), and a working `health` subcommand**,
so that **I can query and manage EventStore from the terminal with scriptable, pipe-friendly output, exit codes suitable for CI/CD gates, and a consistent CLI experience across all future subcommands (FR79, FR80, NFR42)**.

## Acceptance Criteria

1. **Project scaffold** — A new `src/Hexalith.EventStore.Admin.Cli/` console application project exists, targeting .NET 10. It references `Hexalith.EventStore.Admin.Abstractions` for shared models and DTOs. The project is added to `Hexalith.EventStore.slnx`. The project file includes `<PackAsTool>true</PackAsTool>`, `<ToolCommandName>eventstore-admin</ToolCommandName>`, and `<IsPackable>true</IsPackable>`. The `System.CommandLine` package is added to `Directory.Packages.props` and referenced by the project.

2. **Root command** — Running `eventstore-admin` (no subcommand) prints a help message listing available subcommands and global options. The root command description: "Hexalith EventStore administration CLI". Running `eventstore-admin --version` prints the semantic version (from `AssemblyInformationalVersionAttribute`, which semantic-release populates — NOT `AssemblyVersion` which is always `1.0.0.0`). Running `eventstore-admin --help` prints formatted help text.

3. **Global options** — Four global options are available on every subcommand:
   - `--url <url>` (alias `-u`): Admin API base URL. Default: `http://localhost:5002`. Environment variable fallback: `EVENTSTORE_ADMIN_URL`.
   - `--token <token>` (alias `-t`): JWT Bearer token or API key for authentication. No default. Environment variable fallback: `EVENTSTORE_ADMIN_TOKEN`.
   - `--format <format>` (alias `-f`): Output format — `json`, `csv`, or `table`. Default: `table`. Environment variable fallback: `EVENTSTORE_ADMIN_FORMAT`. Invalid values (e.g., `--format xml`) are rejected by `System.CommandLine`'s `FromAmong("json", "csv", "table")` validator, which prints the built-in error message and usage help automatically.
   - `--output <file>` (alias `-o`): Redirect output to file path instead of stdout. Default: null (stdout). No environment variable fallback.
   Options are parsed before subcommand execution and available to all command handlers via a shared `GlobalOptions` record.

4. **Environment variable fallback** — Global options resolve in order: (1) explicit CLI argument, (2) environment variable, (3) default value. When `--url` is not provided and `EVENTSTORE_ADMIN_URL` is set, the environment variable value is used. Same for `--token` and `--format`.

5. **Output formatting** — An `IOutputFormatter` abstraction supports three formats:
   - `json`: Serialized via `System.Text.Json` with `JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }`. Enums serialize as camelCase strings (e.g., `"healthy"`, `"degraded"`, `"unhealthy"`), NOT integers. Single objects and collections both supported.
   - `csv`: Header row + data rows, comma-separated, values quoted only when containing commas or quotes. Collections render as multi-row CSV. Single objects render as key-value pairs (two columns: Property, Value).
   - `table`: Human-readable aligned columns with header separator. When a `ColumnDefinition` list is provided, use it for column headers, property extraction, max widths, and alignment. When no `ColumnDefinition` list is provided, auto-discover **scalar** properties via reflection (string, numeric, bool, enum, DateTime/DateTimeOffset — skip `IEnumerable<T>` except string, skip nested complex types) and auto-calculate column widths by scanning all values for the longest string per column. Truncate values longer than column max width (or terminal width if no max specified) with `...`. When stdout is not a terminal (piped), omit ANSI formatting.
   Output writes to stdout by default, or to the file specified by `--output`. The `--output` file is overwritten if it exists. UTF-8 encoding without BOM.

6. **Exit codes** — All commands return standardized exit codes per UX-DR52:
   - `0`: Success / healthy
   - `1`: Degraded / warning (partial success, non-critical issues)
   - `2`: Critical / error (command failed, connectivity issues, auth failures)
   Exit codes are defined in a static `ExitCodes` class. Command handlers return `Task<int>` where the int is the exit code. Unhandled exceptions are caught by a global handler that prints a user-friendly error to stderr and returns exit code `2`.

7. **Admin API HTTP client** — An `AdminApiClient` class wraps `HttpClient` for calling the Admin REST API. Two constructors: (a) public `AdminApiClient(GlobalOptions options)` creates its own `HttpClient`, (b) internal `AdminApiClient(HttpClient httpClient)` accepts a pre-configured client for unit testing. Configuration: base URL from `--url` global option, JWT Bearer token from `--token` added as `Authorization: Bearer {token}` header when provided. JSON deserialization uses `System.Text.Json` with the same `JsonSerializerOptions` as output formatting (including `JsonStringEnumConverter`). All error messages include the resolved URL to confirm environment variable resolution. Error handling:
   - HTTP 401 → "Authentication required. Use --token to provide a JWT token. (URL: {resolvedUrl})" to stderr, exit code `2`.
   - HTTP 403 → "Access denied. Insufficient permissions. (URL: {resolvedUrl})" to stderr, exit code `2`.
   - HTTP 404 → "Endpoint not found at {resolvedUrl}{path}. Verify the Admin API version matches the CLI version." to stderr, exit code `2`.
   - HTTP 5xx → "Admin API server error: {statusCode}. (URL: {resolvedUrl})" to stderr, exit code `2`.
   - Connection refused → "Cannot connect to Admin API at {resolvedUrl}. Is the server running?" to stderr, exit code `2`.
   - Timeout (10s default) → "Request timed out after 10 seconds. (URL: {resolvedUrl})" to stderr, exit code `2`.
   - `JsonException` (deserialization failure) → "Invalid response from Admin API. Possible version mismatch between CLI and server." to stderr, exit code `2`.
   - `--output` file errors (`IOException`, `UnauthorizedAccessException`) → "Cannot write to {filePath}: {message}" to stderr, exit code `2`.

8. **Health subcommand** — `eventstore-admin health` calls `GET /api/v1/admin/health` and displays the system health report. The `HealthCommand` handler calls the formatter TWICE to render two sections (not through a single `Format<T>` call):
   - **Section 1 — Status overview:** Rendered as a single-object key/value table: Overall Status (`OverallStatus` enum), Total Events (`TotalEventCount`), Events/sec (`EventsPerSecond` formatted to 1 decimal), Error % (`ErrorPercentage` formatted to 2 decimals), DAPR Components (`DaprComponents.Count`), Healthy (`DaprComponents.Count(c => c.Status == Healthy)`), Degraded (count), Unhealthy (count). Use `formatter.Format(overviewObject)` with a constructed anonymous/DTO object.
   - **Section 2 — Component detail:** Rendered as a collection table with columns: Component Name (`ComponentName`), Type (`ComponentType`), Status (`Status` enum), Last Check (`LastCheckUtc` formatted as relative time or ISO 8601). Use `formatter.FormatCollection(report.DaprComponents, columns)`.
   - **JSON format:** Raw `SystemHealthReport` serialized as a single JSON object (one formatter call).
   - **CSV format:** Component detail rows only (one formatter call on `DaprComponents`).
   - **Exit code:** `0` if `OverallStatus == Healthy`, `1` if `Degraded`, `2` if `Unhealthy`.

9. **Subcommand stubs** — The following subcommands exist as placeholders that print "Not yet implemented. Coming in a future release." and return exit code `0`: `stream`, `projection`, `tenant`, `snapshot`, `backup`, `config`. Each stub has a `--help` description matching UX-DR50. The stubs establish the command tree structure for future stories (17-2 through 17-7).

10. **Startup performance** — The CLI starts and returns results for `eventstore-admin health` (against a local server) within 3 seconds including .NET runtime startup (NFR42). No unnecessary DI container, no Aspire dependencies, no DAPR SDK. Minimal startup path: parse args → create HttpClient → make request → format output.

11. **Error output** — All error messages and diagnostic output go to stderr (`Console.Error`), never stdout. This ensures stdout contains only the command's data output and is safe to pipe to other tools or redirect to files. When `--output` is specified, only data goes to the file; errors still go to stderr.

12. **Test project** — A new `tests/Hexalith.EventStore.Admin.Cli.Tests/` xUnit test project exists, added to `Hexalith.EventStore.slnx`. Tests cover: global option parsing, environment variable fallback, output formatting (JSON/CSV/table), exit code mapping, health command output formatting. Tests use xUnit + Shouldly per project conventions.

## Tasks / Subtasks

- [x] **Task 1: Project scaffold and solution setup** (AC: 1)
  - [x] 1.1 Add `System.CommandLine` package to `Directory.Packages.props`. Run `dotnet package search System.CommandLine --prerelease --take 1` to find the latest version and pin it. `System.CommandLine` is still in preview but is the de facto standard. Do NOT add `System.CommandLine.Hosting` — we avoid DI/hosting for startup performance (NFR42).
  - [x] 1.2 Create `src/Hexalith.EventStore.Admin.Cli/Hexalith.EventStore.Admin.Cli.csproj`:
    ```xml
    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
        <PackAsTool>true</PackAsTool>
        <ToolCommandName>eventstore-admin</ToolCommandName>
        <IsPackable>true</IsPackable>
        <RootNamespace>Hexalith.EventStore.Admin.Cli</RootNamespace>
        <AssemblyName>Hexalith.EventStore.Admin.Cli</AssemblyName>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="System.CommandLine" />
        <ProjectReference Include="..\Hexalith.EventStore.Admin.Abstractions\Hexalith.EventStore.Admin.Abstractions.csproj" />
      </ItemGroup>
    </Project>
    ```
  - [x] 1.3 Create `tests/Hexalith.EventStore.Admin.Cli.Tests/Hexalith.EventStore.Admin.Cli.Tests.csproj` following the pattern of `tests/Hexalith.EventStore.Admin.UI.Tests/`. Reference xUnit, Shouldly, coverlet.collector from `Directory.Packages.props`.
  - [x] 1.4 Add both projects to `Hexalith.EventStore.slnx`. Follow the existing XML format. Place CLI in `src/` folder and tests in `tests/` folder.

- [x] **Task 2: Root command, global options, and Program.cs** (AC: 2, 3, 4)
  - [x] 2.1 Create `Program.cs` with the `System.CommandLine` root command setup. The `Option<T>` instances must be accessible to all command factory methods. Pattern: `GlobalOptionsBinding.Create()` returns a record containing all four `Option<T>` instances. `Program.cs` adds them to the root command and passes them to each command factory:
    ```csharp
    var binding = GlobalOptionsBinding.Create();
    var rootCommand = new RootCommand("Hexalith EventStore administration CLI");
    rootCommand.AddGlobalOption(binding.UrlOption);
    rootCommand.AddGlobalOption(binding.TokenOption);
    rootCommand.AddGlobalOption(binding.FormatOption);
    rootCommand.AddGlobalOption(binding.OutputOption);
    rootCommand.AddCommand(HealthCommand.Create(binding));
    rootCommand.AddCommand(StubCommands.Create("stream", "Query, list, and inspect event streams"));
    // ... other stubs
    return await rootCommand.InvokeAsync(args);
    ```
    Use `AddGlobalOption` (not `AddOption`) so options propagate to all subcommands automatically.
  - [x] 2.2 Create `GlobalOptions.cs` — a record holding parsed global option values:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli;
    public record GlobalOptions(string Url, string? Token, string Format, string? OutputFile);
    ```
  - [x] 2.3 Create `GlobalOptionsBinding.cs` — factory that creates all `Option<T>` instances and returns them in a record for sharing with command factories:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli;

    public record GlobalOptionsBinding(
        Option<string> UrlOption,
        Option<string?> TokenOption,
        Option<string> FormatOption,
        Option<string?> OutputOption)
    {
        public static GlobalOptionsBinding Create()
        {
            var urlOption = new Option<string>("--url", () =>
                Environment.GetEnvironmentVariable("EVENTSTORE_ADMIN_URL") ?? "http://localhost:5002",
                "Admin API base URL");
            urlOption.AddAlias("-u");

            var tokenOption = new Option<string?>("--token", () =>
                Environment.GetEnvironmentVariable("EVENTSTORE_ADMIN_TOKEN"),
                "JWT Bearer token or API key");
            tokenOption.AddAlias("-t");

            var formatOption = new Option<string>("--format", () =>
                Environment.GetEnvironmentVariable("EVENTSTORE_ADMIN_FORMAT") ?? "table",
                "Output format");
            formatOption.AddAlias("-f");
            formatOption.FromAmong("json", "csv", "table");

            var outputOption = new Option<string?>("--output", "Redirect output to file");
            outputOption.AddAlias("-o");

            return new GlobalOptionsBinding(urlOption, tokenOption, formatOption, outputOption);
        }

        public GlobalOptions Resolve(InvocationContext context) => new(
            context.ParseResult.GetValueForOption(UrlOption)!,
            context.ParseResult.GetValueForOption(TokenOption),
            context.ParseResult.GetValueForOption(FormatOption)!,
            context.ParseResult.GetValueForOption(OutputOption));
    }
    ```
    Command handlers call `binding.Resolve(context)` to get the fully-resolved `GlobalOptions` record.
  - [x] 2.4 Wire `--version` to print the semantic version. Use `Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion` which contains the SemVer set by semantic-release (e.g., `1.2.3`), NOT `Assembly.GetName().Version` which is always `1.0.0.0`. System.CommandLine's built-in version option reads `AssemblyInformationalVersionAttribute` by default — verify this works and no custom handler is needed.
  - [x] 2.5 Add global exception handler: catch all unhandled exceptions, print user-friendly error message to stderr, return exit code 2.

- [x] **Task 3: Output formatting infrastructure** (AC: 5, 11)
  - [x] 3.1 Create `IOutputFormatter.cs` interface:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli.Formatting;
    public interface IOutputFormatter
    {
        string Format<T>(T item, IReadOnlyList<ColumnDefinition>? columns = null);
        string FormatCollection<T>(IReadOnlyList<T> items, IReadOnlyList<ColumnDefinition>? columns = null);
    }
    ```
  - [x] 3.2 Create `ColumnDefinition.cs` record and `Alignment` enum for table/CSV column metadata:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli.Formatting;

    public enum Alignment { Left, Right }

    public record ColumnDefinition(string Header, string PropertyName, int? MaxWidth = null, Alignment Align = Alignment.Left);
    ```
  - [x] 3.3 Create `JsonOutputFormatter.cs` — uses shared `JsonSerializerOptions` with `WriteIndented = true`, `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`, and `Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }`. Define the shared options as a `static readonly` field (reused by `AdminApiClient` for deserialization). `Format<T>` serializes the object. `FormatCollection<T>` serializes the collection as a JSON array. Enums render as camelCase strings: `"healthy"`, `"degraded"`, `"unhealthy"`.
  - [x] 3.4 Create `CsvOutputFormatter.cs` — header row + data rows. Values quoted when containing commas, quotes, or newlines. Single objects render as two-column key/value. Uses `ColumnDefinition` list when provided; otherwise auto-discovers **scalar** properties via reflection (same scalar-only rule as table formatter — skip `IEnumerable<T>` except string, skip nested complex types). Enums render as their string name (not integer).
  - [x] 3.5 Create `TableOutputFormatter.cs` — auto-width columns, header separator with dashes, left/right alignment per `ColumnDefinition`. Truncate values exceeding column max width with `...`. When stdout is piped (not a terminal), omit any ANSI color codes. Detect via `Console.IsOutputRedirected`.
  - [x] 3.6 Create `OutputFormatterFactory.cs` — returns the correct `IOutputFormatter` based on format string:
    ```csharp
    public static IOutputFormatter Create(string format) => format.ToLowerInvariant() switch
    {
        "json" => new JsonOutputFormatter(),
        "csv" => new CsvOutputFormatter(),
        "table" => new TableOutputFormatter(),
        _ => throw new ArgumentException($"Unknown output format: {format}")
    };
    ```
  - [x] 3.7 Create `OutputWriter.cs` — writes formatted output to stdout or to file (from `--output`). Error messages always go to `Console.Error`. Uses UTF-8 without BOM for file output. Catches `IOException` and `UnauthorizedAccessException` when writing to file → prints "Cannot write to {path}: {message}" to stderr and returns exit code `2`.

- [x] **Task 4: Admin API HTTP client and health command** (AC: 7, 8)
  - [x] 4.1 Create `AdminApiClient.cs` — wraps `HttpClient` for API calls. Two constructors for testability:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli.Client;
    public class AdminApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;

        public AdminApiClient(GlobalOptions options)
        {
            _httpClient = new HttpClient { BaseAddress = new Uri(options.Url), Timeout = TimeSpan.FromSeconds(10) };
            if (options.Token is not null)
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        }

        internal AdminApiClient(HttpClient httpClient) => _httpClient = httpClient; // For unit tests

        public async Task<T> GetAsync<T>(string path, CancellationToken ct) { ... }
    }
    ```
    Uses the shared `JsonSerializerOptions` (with `JsonStringEnumConverter`) for deserialization. Handle errors per AC 7 including HTTP 404, `JsonException`, and file I/O errors. All error messages include the resolved base URL.
  - [x] 4.2 Create `HealthCommand.cs` — `eventstore-admin health` subcommand. Accepts `GlobalOptionsBinding` to resolve options:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli.Commands;
    public static class HealthCommand
    {
        public static Command Create(GlobalOptionsBinding binding) { ... }
    }
    ```
    Calls `GET /api/v1/admin/health`, deserializes `SystemHealthReport` (from Admin.Abstractions). Maps `OverallStatus` to exit code: `Healthy`→0, `Degraded`→1, `Unhealthy`→2. Formats output by calling the formatter TWICE for table format:
    - **Table section 1:** Construct a status overview object with computed values: Overall Status, Total Events, Events/sec, Error %, Component Count (`DaprComponents.Count`), Healthy count, Degraded count, Unhealthy count. Render via `formatter.Format(overview)`.
    - **Table section 2:** Render `DaprComponents` as a collection table with columns: Component Name, Type, Status, Last Check. Render via `formatter.FormatCollection(report.DaprComponents, columns)`. Print a blank line between sections.
    - **JSON:** Single `formatter.Format(report)` call — raw `SystemHealthReport` serialized.
    - **CSV:** Single `formatter.FormatCollection(report.DaprComponents, columns)` — component detail rows only.
  - [x] 4.3 Create `ExitCodes.cs` — static class with constants:
    ```csharp
    namespace Hexalith.EventStore.Admin.Cli;
    public static class ExitCodes
    {
        public const int Success = 0;
        public const int Degraded = 1;
        public const int Error = 2;
    }
    ```

- [x] **Task 5: Subcommand stubs** (AC: 9)
  - [x] 5.1 Create stub commands for: `stream`, `projection`, `tenant`, `snapshot`, `backup`, `config`. Each is a `Command` with a description and a handler that prints "Not yet implemented. Coming in a future release." to stderr and returns exit code `0`. Descriptions:
    - `stream`: "Query, list, and inspect event streams"
    - `projection`: "List, pause, resume, and reset projections"
    - `tenant`: "List tenants, view quotas, and verify isolation"
    - `snapshot`: "Manage aggregate snapshots"
    - `backup`: "Trigger and manage backups"
    - `config`: "Manage connection profiles and CLI configuration"
  - [x] 5.2 Register all stubs and the health command on the root command in `Program.cs`.

- [x] **Task 6: Unit tests** (AC: 12)
  - [x] **Global options tests:**
  - [x] 6.1 `GlobalOptions_DefaultValues_AreCorrect` — Default URL is `http://localhost:5002`, format is `table`, token is null, output is null.
  - [x] 6.2 `GlobalOptions_ExplicitArguments_OverrideDefaults` — `--url https://prod:8080 --token abc --format json` parses correctly.
  - [x] 6.3 `GlobalOptions_EnvironmentVariables_FallbackWhenNoArgument` — `EVENTSTORE_ADMIN_URL` used when `--url` not provided. **Test isolation:** This test and 6.4 MUST NOT run in parallel — use `[Collection("EnvironmentVariableTests")]` on the test class. Clean up env vars in `Dispose()`.
  - [x] 6.4 `GlobalOptions_ExplicitArgument_OverridesEnvironmentVariable` — `--url` takes precedence over env var. Same `[Collection("EnvironmentVariableTests")]`.
  - [x] 6.5 `GlobalOptions_InvalidFormat_ReturnsError` — `--format xml` produces an error via `FromAmong` validation.
  - [x] **Output formatting tests:**
  - [x] 6.6 `JsonFormatter_SingleObject_ReturnsIndentedJson` — Correct JSON output with camelCase.
  - [x] 6.7 `JsonFormatter_Collection_ReturnsJsonArray` — Array of objects.
  - [x] 6.8 `CsvFormatter_Collection_ReturnsHeaderAndRows` — Correct CSV with headers.
  - [x] 6.9 `CsvFormatter_ValuesWithCommas_AreQuoted` — Quoting rules applied.
  - [x] 6.10 `CsvFormatter_SingleObject_ReturnsKeyValuePairs` — Two-column format.
  - [x] 6.11 `TableFormatter_Collection_ReturnsAlignedColumns` — Correct alignment and separator.
  - [x] 6.12 `TableFormatter_LongValues_AreTruncated` — Truncation with ellipsis.
  - [x] **Exit code tests:**
  - [x] 6.13 `ExitCodes_Values_MatchConvention` — Success=0, Degraded=1, Error=2.
  - [x] **Health command tests:**
  - [x] 6.14 `HealthCommand_HealthyReport_ReturnsExitCode0` — Healthy status maps to 0.
  - [x] 6.15 `HealthCommand_DegradedReport_ReturnsExitCode1` — Degraded maps to 1.
  - [x] 6.16 `HealthCommand_UnhealthyReport_ReturnsExitCode2` — Unhealthy maps to 2.
  - [x] 6.17 `HealthCommand_TableFormat_ShowsOverviewAndComponents` — Table output contains both sections: status overview with computed counts (Overall Status, Total Events, healthy/degraded/unhealthy component counts) AND component detail table (ComponentName, ComponentType, Status, LastCheckUtc).
  - [x] 6.18 `HealthCommand_JsonFormat_ReturnsValidJson` — JSON output deserializes back to `SystemHealthReport` with matching `OverallStatus`, `TotalEventCount`, `DaprComponents`.
  - [x] 6.19 `HealthCommand_CsvFormat_ReturnsComponentRows` — CSV has header row (ComponentName, ComponentType, Status, LastCheckUtc) and one data row per `DaprComponentHealth` entry.
  - [x] **API client tests:**
  - [x] 6.20 `AdminApiClient_SetsBaseUrl_FromGlobalOptions` — HttpClient.BaseAddress set correctly.
  - [x] 6.21 `AdminApiClient_AddsAuthHeader_WhenTokenProvided` — Authorization header present.
  - [x] 6.22 `AdminApiClient_NoAuthHeader_WhenTokenNull` — No Authorization header.
  - [x] 6.23 `AdminApiClient_Http401_PrintsAuthError` — Correct stderr message.
  - [x] 6.24 `AdminApiClient_ConnectionRefused_PrintsConnectError` — Correct stderr message including resolved URL.
  - [x] 6.25 `AdminApiClient_Http404_PrintsEndpointNotFound` — "Endpoint not found" message with path.
  - [x] 6.26 `AdminApiClient_JsonException_PrintsVersionMismatch` — "Invalid response" message on malformed JSON.
  - [x] **JSON enum serialization tests:**
  - [x] 6.27 `JsonFormatter_EnumValues_SerializeAsStrings` — `HealthStatus.Healthy` serializes as `"healthy"`, not `0`. Verifies `JsonStringEnumConverter` is active.
  - [x] **Stub command tests:**
  - [x] 6.28 `StubCommands_PrintNotImplemented_AndReturnZero` — All stubs print message and return 0.

  Test file location: `tests/Hexalith.EventStore.Admin.Cli.Tests/`

  **HTTP client mock pattern:** `HttpMessageHandler.SendAsync` is `protected`, so NSubstitute cannot mock it directly. Create a `MockHttpMessageHandler` helper class in the test project:
  ```csharp
  internal class MockHttpMessageHandler : HttpMessageHandler
  {
      private readonly HttpResponseMessage _response;
      public HttpRequestMessage? LastRequest { get; private set; }

      public MockHttpMessageHandler(HttpResponseMessage response) => _response = response;

      protected override Task<HttpResponseMessage> SendAsync(
          HttpRequestMessage request, CancellationToken cancellationToken)
      {
          LastRequest = request;
          return Task.FromResult(_response);
      }
  }
  ```
  Pass this handler to `new HttpClient(handler)` in tests. For connection-refused tests, throw `HttpRequestException` from `SendAsync`. This pattern avoids NSubstitute limitations with protected methods.

## Dev Notes

### This is a GREENFIELD project — no existing code to modify (except solution and package props)

All code in `src/Hexalith.EventStore.Admin.Cli/` is new. The only existing files to modify are:
- `Directory.Packages.props` — add `System.CommandLine` package version
- `Hexalith.EventStore.slnx` — add CLI and CLI.Tests project references

### System.CommandLine is still in preview

`System.CommandLine` has been in preview since 2020. Run `dotnet package search System.CommandLine --prerelease --take 1` to find the latest version at implementation time and pin it in `Directory.Packages.props`. Despite the beta label, it is the de facto standard for .NET CLI tools and is used by `dotnet` itself. Key API patterns:
- `RootCommand` for the top-level command
- `Command` for subcommands
- `Option<T>` for typed options with aliases, `SetDefaultValueFactory` for env var fallback
- `option.FromAmong(...)` for value validation
- `Argument<T>` for positional arguments
- `command.SetHandler(async (InvocationContext context) => { ... })` — use the `InvocationContext` overload, which is stable across preview versions
- `context.ParseResult.GetValueForOption(option)` to retrieve parsed option values
- `context.ExitCode = N` to set the exit code (do NOT return int from handler — set on context)

**Handler wiring example (stable pattern):**
```csharp
// In HealthCommand.Create(GlobalOptionsBinding binding):
var command = new Command("health", "Show system health status");
command.SetHandler(async (InvocationContext context) =>
{
    var options = binding.Resolve(context); // Uses the shared Option<T> instances
    using var client = new AdminApiClient(options);
    var formatter = OutputFormatterFactory.Create(options.Format);
    var writer = new OutputWriter(options.OutputFile);
    context.ExitCode = await ExecuteAsync(client, formatter, writer, context.GetCancellationToken());
});
return command;
```

### Existing Admin.Abstractions models to reuse

The CLI consumes the same DTOs as the Web UI and Server. Key models for the health command:
- `SystemHealthReport(OverallStatus, TotalEventCount, EventsPerSecond, ErrorPercentage, DaprComponents, ObservabilityLinks)` — overall health
- `HealthStatus` — enum: `Healthy`, `Degraded`, `Unhealthy`
- `DaprComponentHealth(ComponentName, ComponentType, Status, LastCheckUtc)` — per-component status
- `ObservabilityLinks(TracingUrl, MetricsUrl, LoggingUrl)` — deep links to external tools

**Model shape matters for AC 8.** The `SystemHealthReport` does NOT have "Component Count" or "Latency" properties. The health command must compute derived values (e.g., healthy count = `DaprComponents.Count(c => c.Status == Healthy)`) and construct a view object for the status overview section.

Do NOT recreate these models. Import them from the Abstractions project reference.

### Future: Admin.Client shared library (Epic 18 consideration)

The `AdminApiClient` in this story lives in `Admin.Cli/Client/`. When Epic 18 (MCP Server) begins, both CLI and MCP will need identical HTTP client logic (base URL, auth header, error handling, JSON deserialization). At that point, extract the shared client into `Hexalith.EventStore.Admin.Client` — a thin HTTP client package referenced by both CLI and MCP. For now, keeping it in the CLI project is correct (no premature abstraction). This is a known duplication point flagged for Epic 18 story creation.

### Admin API endpoints (already implemented in Admin.Server)

The CLI calls the same REST API as the Web UI. For story 17-1, only the health endpoint is needed:

| Method | Route | Auth Policy | Returns |
|--------|-------|-------------|---------|
| GET | `api/v1/admin/health` | ReadOnly | `SystemHealthReport` |

The full endpoint list (for future stories) is documented in the Admin.Server controllers at `src/Hexalith.EventStore.Admin.Server/Controllers/`.

### Architecture: CLI is a thin HTTP client (ADR-P4)

Per ADR-P4, the CLI never accesses DAPR directly. It calls the Admin API over HTTP. This means:
- No DAPR SDK dependency
- No Aspire dependency
- No DI container needed (keep startup minimal for NFR42 — 3s startup target)
- Simple `HttpClient` with `System.Text.Json` deserialization
- Authentication via JWT Bearer token passed in `--token` or `EVENTSTORE_ADMIN_TOKEN`

### Do NOT use Hosting or DI containers

For startup performance (NFR42), the CLI should NOT use `Microsoft.Extensions.Hosting` or `IServiceCollection`. Create dependencies directly in command handlers. The startup path should be: parse arguments → create `HttpClient` → execute command → format output → exit. Avoid any DI framework overhead.

### Shared JsonSerializerOptions — single source of truth

Define a `static readonly JsonSerializerOptions` in a shared location (e.g., `JsonDefaults.Options`) used by BOTH `JsonOutputFormatter` for serialization AND `AdminApiClient` for deserialization. This ensures enum handling is consistent everywhere:
```csharp
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
```
Without `JsonStringEnumConverter`, `HealthStatus.Healthy` serializes as `0` — completely breaking `jq` scriptability.

### Reflection-based property auto-discovery — scalar types only

When `ColumnDefinition` is not provided, formatters auto-discover properties via reflection. Only include **scalar** types as columns:
- Included: `string`, `int`, `long`, `double`, `decimal`, `float`, `bool`, `DateTime`, `DateTimeOffset`, `Guid`, enums
- Excluded: `IEnumerable<T>` (except `string`), nested records/classes, delegates, `object`

This prevents `SystemHealthReport.DaprComponents` (an `IReadOnlyList<DaprComponentHealth>`) from appearing as a garbled cell like `System.Collections.Generic.List'1[...]`. Complex properties require explicit `ColumnDefinition` lists — which the health command already provides.

### Output formatting is critical for scriptability

The CLI must be pipe-friendly (FR80). Key rules:
- stdout = data only (formatted output)
- stderr = errors, diagnostics, progress messages
- `--format json` must produce valid JSON that `jq` can parse
- `--format csv` must produce valid CSV that `cut`/`awk`/Excel can consume
- `--format table` is for human readability only
- `--output <file>` redirects data to file, errors still go to stderr
- When stdout is piped (not a terminal), omit ANSI codes from table output

### .NET global tool packaging

The project file must include:
```xml
<PackAsTool>true</PackAsTool>
<ToolCommandName>eventstore-admin</ToolCommandName>
```
This allows installation via `dotnet tool install -g Hexalith.EventStore.Admin.Cli`. The tool command name is `eventstore-admin` per UX-DR50. The package will be published to NuGet alongside the other Hexalith.EventStore packages via the existing CI/CD pipeline (Epic 8 semantic-release).

### Code style — follow project conventions exactly

- File-scoped namespaces: `namespace Hexalith.EventStore.Admin.Cli;`
- Allman braces (opening brace on new line)
- Private fields: `_camelCase`
- Async methods: `Async` suffix
- 4 spaces indentation, CRLF line endings, UTF-8
- Nullable enabled
- Implicit usings enabled
- Warnings as errors

### Previous story intelligence

Story 16-6 (Dead-Letter Queue Manager) established patterns for:
- API client structure (typed HTTP client with error handling)
- Test patterns (bUnit for UI, xUnit + Shouldly for unit tests)
- DI registration patterns
- The Admin API surface is stable and complete

The CLI follows a different pattern (no Blazor, no DI container) but reuses the same API endpoints and Abstractions models.

### Git commit patterns from recent work

Recent commits follow: `feat: <description> for story <story-id>`
Branch naming: `feat/story-17-1-cli-scaffold-system-commandline-global-options`

### Project Structure Notes

New files (all new — no existing files to replace):
```
src/Hexalith.EventStore.Admin.Cli/
  Hexalith.EventStore.Admin.Cli.csproj
  Program.cs
  GlobalOptions.cs
  GlobalOptionsBinding.cs
  ExitCodes.cs
  Client/
    AdminApiClient.cs
  Commands/
    HealthCommand.cs
    StubCommands.cs
  Formatting/
    IOutputFormatter.cs
    ColumnDefinition.cs
    JsonOutputFormatter.cs
    CsvOutputFormatter.cs
    TableOutputFormatter.cs
    OutputFormatterFactory.cs
    OutputWriter.cs

tests/Hexalith.EventStore.Admin.Cli.Tests/
  Hexalith.EventStore.Admin.Cli.Tests.csproj
  GlobalOptionsTests.cs
  Formatting/
    JsonOutputFormatterTests.cs
    CsvOutputFormatterTests.cs
    TableOutputFormatterTests.cs
  Commands/
    HealthCommandTests.cs
  Client/
    AdminApiClientTests.cs
  StubCommandsTests.cs
```

Modified files:
- `Directory.Packages.props` — add `System.CommandLine` version
- `Hexalith.EventStore.slnx` — add both projects

### References

- [Source: _bmad-output/planning-artifacts/prd.md — FR79: three admin interfaces (Web UI, CLI, MCP) backed by shared Admin API]
- [Source: _bmad-output/planning-artifacts/prd.md — FR80: JSON/CSV/table output, exit codes, shell completions]
- [Source: _bmad-output/planning-artifacts/prd.md — NFR42: CLI startup + query within 3 seconds]
- [Source: _bmad-output/planning-artifacts/architecture.md — ADR-P4: CLI is thin HTTP client, no DAPR]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR50: subcommand tree]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR51: global options]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR52: exit codes]
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 17 — Admin CLI (eventstore-admin)]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Services/ — service interfaces]
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/ — REST API endpoints]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- System.CommandLine 2.0.0-beta5.25306.1 has breaking API changes from beta4: `SetHandler` → `SetAction`, `InvocationContext` removed, `AddGlobalOption` → `Option.Recursive = true`, `AddAlias` → `Aliases.Add`, `FromAmong` → `AcceptOnlyFromAmong`, constructor 2nd param is `aliases` not `description`. All code adapted to beta5 API.
- CA1062 analyzer required null checks on `GlobalOptionsBinding.Resolve()` and `OutputFormatterFactory.Create()` parameters (TreatWarningsAsErrors).
- Pre-existing build error in `IntegrationTests` (Program class ambiguity between AppHost and Sample) — unrelated to this story.

### Completion Notes List

- **Task 1:** Created `src/Hexalith.EventStore.Admin.Cli` project (Exe, PackAsTool, ToolCommandName=eventstore-admin) referencing Admin.Abstractions. Created `tests/Hexalith.EventStore.Admin.Cli.Tests` test project. Added `System.CommandLine 2.0.0-beta5.25306.1` to `Directory.Packages.props`. Both projects added to `Hexalith.EventStore.slnx`.
- **Task 2:** Created `Program.cs` with RootCommand, `GlobalOptionsBinding` factory with env-var fallback and `FromAmong` validation, `GlobalOptions` record, `ExitCodes` static class. `--version` uses built-in AssemblyInformationalVersionAttribute. Global exception handler catches unhandled errors.
- **Task 3:** Created `IOutputFormatter` interface, `ColumnDefinition` record, `JsonOutputFormatter` (indented camelCase with JsonStringEnumConverter), `CsvOutputFormatter` (header+rows, quoting, key-value single objects), `TableOutputFormatter` (auto-width columns, header separator, truncation with `...`), `OutputFormatterFactory`, `OutputWriter` (stdout/file with error handling), `JsonDefaults` (shared options).
- **Task 4:** Created `AdminApiClient` wrapping HttpClient with error handling for 401/403/404/5xx/connection-refused/timeout/JSON-parse errors. Created `HealthCommand` calling GET /api/v1/admin/health with two-section table format (overview + components), JSON, and CSV output modes. Exit codes mapped from OverallStatus.
- **Task 5:** Created `StubCommands` factory for stream, projection, tenant, snapshot, backup, config. All print "Not yet implemented. Coming in a future release." to stderr and return exit code 0. Registered on root command in Program.cs.
- **Task 6:** Created 34 unit tests covering: global option defaults/parsing/env-var-fallback/validation (5), JSON/CSV/table formatting (7), exit codes (1), health command output/exit-codes (6), API client auth/errors (8), JSON enum serialization (1), stub commands (6). All tests pass.
- **Post-review fixes:** Applied code review patch pass: health output now writes atomically and propagates file-write exit code failures; table formatter handles empty single-object projection safely; API client maps both timeout and cancellation to user-friendly exceptions; added deterministic options-constructor auth-header test coverage.

### File List

**New files:**

- src/Hexalith.EventStore.Admin.Cli/Hexalith.EventStore.Admin.Cli.csproj
- src/Hexalith.EventStore.Admin.Cli/Program.cs
- src/Hexalith.EventStore.Admin.Cli/GlobalOptions.cs
- src/Hexalith.EventStore.Admin.Cli/GlobalOptionsBinding.cs
- src/Hexalith.EventStore.Admin.Cli/ExitCodes.cs
- src/Hexalith.EventStore.Admin.Cli/Client/AdminApiClient.cs
- src/Hexalith.EventStore.Admin.Cli/Client/AdminApiException.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/HealthCommand.cs
- src/Hexalith.EventStore.Admin.Cli/Commands/StubCommands.cs
- src/Hexalith.EventStore.Admin.Cli/Formatting/IOutputFormatter.cs
- src/Hexalith.EventStore.Admin.Cli/Formatting/ColumnDefinition.cs
- src/Hexalith.EventStore.Admin.Cli/Formatting/JsonDefaults.cs
- src/Hexalith.EventStore.Admin.Cli/Formatting/JsonOutputFormatter.cs
- src/Hexalith.EventStore.Admin.Cli/Formatting/CsvOutputFormatter.cs
- src/Hexalith.EventStore.Admin.Cli/Formatting/TableOutputFormatter.cs
- src/Hexalith.EventStore.Admin.Cli/Formatting/OutputFormatterFactory.cs
- src/Hexalith.EventStore.Admin.Cli/Formatting/OutputWriter.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Hexalith.EventStore.Admin.Cli.Tests.csproj
- tests/Hexalith.EventStore.Admin.Cli.Tests/GlobalOptionsTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/ExitCodesTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/StubCommandsTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Formatting/JsonOutputFormatterTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Formatting/CsvOutputFormatterTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Formatting/TableOutputFormatterTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/HealthCommandTests.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Client/MockHttpMessageHandler.cs
- tests/Hexalith.EventStore.Admin.Cli.Tests/Client/AdminApiClientTests.cs

**Modified files:**

- Directory.Packages.props (added System.CommandLine 2.0.0-beta5.25306.1)
- Hexalith.EventStore.slnx (added Admin.Cli and Admin.Cli.Tests projects)

**Modified workflow artifacts:**

- _bmad-output/implementation-artifacts/17-1-cli-scaffold-system-commandline-global-options.md (status, tasks, dev agent record)
- _bmad-output/implementation-artifacts/sprint-status.yaml (story status)

### Change Log

- 2026-03-25: Story 17-1 implementation complete — CLI scaffold with System.CommandLine 2.0.0-beta5, root command with 4 global options (--url, --token, --format, --output), env-var fallback, 3 output formatters (JSON/CSV/table), AdminApiClient with comprehensive error handling, health subcommand, 6 stub subcommands, and post-review hardening for output/error propagation and cancellation handling. 34 unit tests passing.
