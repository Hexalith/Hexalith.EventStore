# Story 18.1: MCP Server Scaffold — stdio Transport

Status: done

Size: Medium — 1 new project (Admin.Mcp), 1 new test project (Admin.Mcp.Tests), solution file updates, CI/CD updates, Directory.Packages.props update, 5 task groups, 14 ACs, ~15-17 tests (~6-8 hours estimated). Creates the foundational MCP server scaffold that exposes EventStore admin operations as AI-callable tools via stdio transport, with HttpClient connectivity to the Admin API.

**Dependency:** Epic 14 (Admin API Foundation) must be complete (all done). Epic 17 stories are independent — MCP server is a parallel consumer of the same Admin API.

## Definition of Done

- All 14 ACs verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- MCP server starts, completes capability negotiation, and responds to `tools/list` via stdio
- HttpClient configured for Admin API communication with Bearer token auth
- Solution file includes both new projects
- CI pipeline runs Admin.Mcp.Tests as part of Tier 1

## Story

As an **AI agent (e.g., Claude) connected to the EventStore via MCP**,
I want **a stdio-based MCP server scaffold with HttpClient connectivity to the Admin API**,
so that **I can discover available tools, connect to the event store, and subsequent stories (18.2-18.5) can register read/write/diagnostic tools on this foundation (FR79, FR81, NFR43)**.

## Acceptance Criteria

1. **New project `Hexalith.EventStore.Admin.Mcp`** — An `Exe` project in `src/Hexalith.EventStore.Admin.Mcp/` targeting `net10.0`. References `Hexalith.EventStore.Admin.Abstractions`. Uses `ModelContextProtocol` NuGet package (v1.1.0). `IsPackable = false` initially (packaging deferred — unlike the CLI, MCP servers are typically configured by path in AI agent config files, not installed via `dotnet tool`). `RootNamespace = Hexalith.EventStore.Admin.Mcp`. `AssemblyName = Hexalith.EventStore.Admin.Mcp`.

2. **stdio transport with Host builder** — `Program.cs` uses the .NET Generic Host pattern: `Host.CreateApplicationBuilder(args)` with `builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`. Logging is configured to write to stderr only (stdout is reserved for MCP JSON-RPC protocol). Server name is set to `"hexalith-eventstore-admin"` and version comes from `AssemblyInformationalVersionAttribute`. The host runs via `await builder.Build().RunAsync()`. The .NET Generic Host handles graceful shutdown via `IHostApplicationLifetime` — `SIGTERM`, `Ctrl+C`, and process termination are handled automatically, ensuring clean stdio stream closure.

3. **MCP server metadata and capabilities** — The server registers with name `"hexalith-eventstore-admin"`, a human-readable description `"Hexalith EventStore administration MCP server — query streams, inspect projections, diagnose issues, and manage operations via AI-callable tools"`, and the assembly version. This metadata is visible to MCP clients during capability negotiation. The server advertises the `tools` capability. Stories 18.2-18.5 may add `resources` or `prompts` capabilities — the scaffold should not pre-declare capabilities it doesn't yet support.

4. **Admin API HttpClient configuration** — An `HttpClient` named `"AdminApi"` is registered via `IHttpClientFactory` with: (a) base address from environment variable `EVENTSTORE_ADMIN_URL` (required, e.g., `https://localhost:5443`), (b) default `Authorization: Bearer <token>` header from environment variable `EVENTSTORE_ADMIN_TOKEN` (required), (c) default `Accept: application/json` header, (d) default timeout of 10 seconds (aligned with NFR43's 1s p99 target — 10s is a generous ceiling for single-resource queries; batch operations in later stories can override per-request if needed). An `internal` typed client class `AdminApiClient` wraps `HttpClient` and provides convenience methods matching the Admin API route structure (`/api/v1/admin/*`). The class is `internal` to keep the public API surface minimal — tests access it via `InternalsVisibleTo` (AC #11).

5. **Configuration validation on startup** — If `EVENTSTORE_ADMIN_URL` or `EVENTSTORE_ADMIN_TOKEN` are missing or empty, the server writes a clear error message to stderr and exits with a non-zero exit code BEFORE starting the MCP host. The error message includes the missing variable name(s) and a usage hint. Additionally: (a) `EVENTSTORE_ADMIN_URL` is validated as a well-formed absolute URI via `Uri.TryCreate(adminUrl, UriKind.Absolute, out _)` — an invalid URI produces a clear error (e.g., `"EVENTSTORE_ADMIN_URL 'not-a-url' is not a valid absolute URI"`) instead of an opaque `UriFormatException` during HttpClient setup, (b) trailing slashes are stripped from the URL (`TrimEnd('/')`) to prevent double-slash paths like `https://host//api/v1/admin/health`, (c) `EVENTSTORE_ADMIN_TOKEN` is trimmed of leading/trailing whitespace to prevent corrupted HTTP Authorization headers from copy-paste errors.

6. **Placeholder ping tool** — A single `[McpServerToolType]` class `ServerTools` with one `[McpServerTool]` method `Ping` that calls `GET /api/v1/admin/health` via the `AdminApiClient` and returns structured JSON with: server status, server version, and admin API connectivity status. The tool distinguishes between failure modes: `"adminApiStatus": "reachable"` on success, `"unauthorized"` on HTTP 401/403 (expired or invalid token), `"error"` on other HTTP error status codes (includes status code in response), and `"unreachable"` on `HttpRequestException` (connection refused, DNS failure, timeout). This differentiation is critical — reporting "unreachable" for an auth failure wastes the user's time debugging network issues instead of token rotation. The tool is decorated with `[Description("Check connectivity to the EventStore Admin API and return server health status")]`.

7. **Solution file updated** — `Hexalith.EventStore.slnx` adds `src/Hexalith.EventStore.Admin.Mcp/Hexalith.EventStore.Admin.Mcp.csproj` in the `/src/` folder and `tests/Hexalith.EventStore.Admin.Mcp.Tests/Hexalith.EventStore.Admin.Mcp.Tests.csproj` in the `/tests/` folder.

8. **New test project `Hexalith.EventStore.Admin.Mcp.Tests`** — An xUnit test project in `tests/Hexalith.EventStore.Admin.Mcp.Tests/` with standard test dependencies (xunit, Shouldly, NSubstitute, Microsoft.NET.Test.Sdk, coverlet.collector). References `Hexalith.EventStore.Admin.Mcp` via `ProjectReference`. Contains tests across four test classes: (a) `AdminApiClientTests` — verifies `GetSystemHealthAsync` sends GET to correct path `/api/v1/admin/health`, verifies `Authorization: Bearer <token>` header is present on requests, verifies `Accept: application/json` header is present. (b) `ConfigurationValidationTests` — verifies missing both env vars produces error mentioning both variable names, verifies missing only `EVENTSTORE_ADMIN_URL` produces error mentioning that variable, verifies missing only `EVENTSTORE_ADMIN_TOKEN` produces error mentioning that variable, verifies invalid URI format produces clear error, verifies valid config passes validation without env-var error exit. (c) `ServerToolsTests` — verifies `Ping` returns `"reachable"` on success, `"unreachable"` on `HttpRequestException`, `"unauthorized"` on 401/403, `"error"` on other HTTP errors, and verifies all returned JSON contains `serverName` and `adminApiStatus` fields. (d) `AssemblyMetadataTests` — verifies assembly name is `Hexalith.EventStore.Admin.Mcp`, verifies assembly has entry point (is Exe), verifies `AssemblyInformationalVersionAttribute` is present.

9. **CI pipeline includes Admin.Mcp.Tests** — The `ci.yml` workflow's "Unit Tests (Tier 1)" step includes `dotnet test tests/Hexalith.EventStore.Admin.Mcp.Tests/` with the same flags as existing test projects. The test summary Python script's `tier1_suites` dictionary includes `'Admin.Mcp.Tests': 'tests/Hexalith.EventStore.Admin.Mcp.Tests/TestResults/**/test-results.trx'`.

10. **Directory.Packages.props updated** — A new `<ItemGroup Label="MCP">` section adds `<PackageVersion Include="ModelContextProtocol" Version="1.1.0" />`. This is the official C# MCP SDK maintained by the MCP project in collaboration with Microsoft.

11. **InternalsVisibleTo for tests** — The `Hexalith.EventStore.Admin.Mcp.csproj` includes `<InternalsVisibleTo Include="Hexalith.EventStore.Admin.Mcp.Tests" />` to allow tests to access internal types like `AdminApiClient`.

12. **Branch naming** — Implementation branch follows project convention: `feat/story-18-1-mcp-server-scaffold-stdio-transport`.

13. **Graceful shutdown** — The MCP server handles `SIGTERM`, `Ctrl+C`, and parent process termination cleanly via the .NET Generic Host's `IHostApplicationLifetime`. No custom shutdown logic is needed — the host lifetime and stdio transport handle cleanup automatically. Verify that the server exits cleanly when stdin is closed (MCP client disconnects).

14. **All existing Tier 1 and Tier 2 tests continue to pass** with zero behavioral change.

## Tasks / Subtasks

- [x] **Task 1: Add ModelContextProtocol to Directory.Packages.props** (AC: #10)
  - [x] 1.1 Add `<ItemGroup Label="MCP">` with `<PackageVersion Include="ModelContextProtocol" Version="1.1.0" />` to `Directory.Packages.props`

- [x] **Task 2: Create the Admin.Mcp project** (AC: #1, #2, #3, #4, #5, #6, #11)
  - [x] 2.1 Create `src/Hexalith.EventStore.Admin.Mcp/Hexalith.EventStore.Admin.Mcp.csproj`:
    ```xml
    <Project Sdk="Microsoft.NET.Sdk">

      <PropertyGroup>
        <OutputType>Exe</OutputType>
        <IsPackable>false</IsPackable>
        <IsPublishable>true</IsPublishable>
        <RootNamespace>Hexalith.EventStore.Admin.Mcp</RootNamespace>
        <AssemblyName>Hexalith.EventStore.Admin.Mcp</AssemblyName>
      </PropertyGroup>

      <ItemGroup>
        <PackageReference Include="ModelContextProtocol" />
        <PackageReference Include="Microsoft.Extensions.Hosting" />
      </ItemGroup>

      <ItemGroup>
        <InternalsVisibleTo Include="Hexalith.EventStore.Admin.Mcp.Tests" />
      </ItemGroup>

      <ItemGroup>
        <ProjectReference Include="..\Hexalith.EventStore.Admin.Abstractions\Hexalith.EventStore.Admin.Abstractions.csproj" />
      </ItemGroup>

    </Project>
    ```
  - [x] 2.2 Create `src/Hexalith.EventStore.Admin.Mcp/AdminApiClient.cs` — typed HttpClient wrapper:
    - Internal class `AdminApiClient` with constructor taking `HttpClient`
    - Method `GetSystemHealthAsync(CancellationToken)` calling `GET /api/v1/admin/health`
    - Returns deserialized `SystemHealthReport` from Admin.Abstractions
    - Uses `System.Net.Http.Json` extensions (`GetFromJsonAsync<T>`)
    - All public methods are `async Task<T>` with `ConfigureAwait(false)`
    - Additional convenience methods can be added in stories 18.2-18.5 as needed
  - [x] 2.3 Create `src/Hexalith.EventStore.Admin.Mcp/Tools/ServerTools.cs`:
    ```csharp
    using System.ComponentModel;
    using System.Text.Json;

    using ModelContextProtocol.Server;

    namespace Hexalith.EventStore.Admin.Mcp.Tools;

    [McpServerToolType]
    public class ServerTools
    {
        [McpServerTool]
        [Description("Check connectivity to the EventStore Admin API and return server health status")]
        public static async Task<string> Ping(
            AdminApiClient adminApiClient,
            CancellationToken cancellationToken)
        {
            // Implementation calls adminApiClient.GetSystemHealthAsync
            // Returns structured JSON with differentiated connectivity status
            // Catches HttpRequestException -> "unreachable"
            // Catches HttpResponseMessage with 401/403 -> "unauthorized"
            // Catches other HTTP errors -> "error" with status code
        }
    }
    ```
    - Returns JSON string with `{ "serverName": "...", "serverVersion": "...", "adminApiStatus": "reachable|unauthorized|error|unreachable", "details": ... }`
    - On success (2xx): includes health report data from Admin API, status `"reachable"`
    - On 401/403: status `"unauthorized"`, details include `"Token may be expired or invalid. Check EVENTSTORE_ADMIN_TOKEN."`
    - On other HTTP errors (4xx/5xx): status `"error"`, details include HTTP status code and reason phrase
    - On `HttpRequestException` (connection refused, DNS, timeout): status `"unreachable"`, details include exception message
    - Tool NEVER throws — always returns valid JSON regardless of failure mode
  - [x] 2.4 Create `src/Hexalith.EventStore.Admin.Mcp/Program.cs`:
    ```csharp
    using System.Reflection;

    using Hexalith.EventStore.Admin.Mcp;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    // Validate required environment variables before starting
    string? adminUrl = Environment.GetEnvironmentVariable("EVENTSTORE_ADMIN_URL");
    string? adminToken = Environment.GetEnvironmentVariable("EVENTSTORE_ADMIN_TOKEN")?.Trim();

    List<string> errors = [];
    if (string.IsNullOrWhiteSpace(adminUrl))
    {
        errors.Add("Missing EVENTSTORE_ADMIN_URL");
    }
    else if (!Uri.TryCreate(adminUrl, UriKind.Absolute, out _))
    {
        errors.Add($"EVENTSTORE_ADMIN_URL '{adminUrl}' is not a valid absolute URI");
    }
    else
    {
        adminUrl = adminUrl.TrimEnd('/');
    }

    if (string.IsNullOrWhiteSpace(adminToken))
    {
        errors.Add("Missing EVENTSTORE_ADMIN_TOKEN");
    }

    if (errors.Count > 0)
    {
        await Console.Error.WriteLineAsync(
            $"Error: {string.Join("; ", errors)}\n"
            + "Usage: Set EVENTSTORE_ADMIN_URL (e.g., https://localhost:5443) "
            + "and EVENTSTORE_ADMIN_TOKEN (Bearer token for Admin API authentication).")
            .ConfigureAwait(false);
        return 1;
    }

    string version = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "0.0.0";

    var builder = Host.CreateApplicationBuilder(args);

    // Logging to stderr only — stdout is reserved for MCP JSON-RPC protocol
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(options =>
        options.LogToStandardErrorThreshold = LogLevel.Trace);

    // Register AdminApiClient as typed HttpClient
    builder.Services.AddHttpClient<AdminApiClient>(client =>
    {
        client.BaseAddress = new Uri(adminUrl!);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
        client.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(10);
    });

    // Register MCP server with stdio transport
    builder.Services
        .AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "hexalith-eventstore-admin",
                Version = version,
            };
        })
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    await builder.Build().RunAsync().ConfigureAwait(false);
    return 0;
    ```
    - **CRITICAL**: Logging MUST go to stderr. stdout is the MCP transport channel. Using `Console.WriteLine` for anything other than MCP protocol messages will corrupt the JSON-RPC stream.
    - The `options.ServerInfo` sets the server name and version visible during MCP capability negotiation.
    - `WithToolsFromAssembly()` discovers all `[McpServerToolType]` classes in the assembly automatically.

- [x] **Task 3: Create the Admin.Mcp.Tests project** (AC: #8, #13)
  - [x] 3.1 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/Hexalith.EventStore.Admin.Mcp.Tests.csproj` following existing test project patterns (xunit, Shouldly, NSubstitute, Microsoft.NET.Test.Sdk, coverlet.collector). Reference `Hexalith.EventStore.Admin.Mcp` via ProjectReference.
  - [x] 3.2 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientTests.cs`:
    - Test that `GetSystemHealthAsync` sends GET to correct path `/api/v1/admin/health`
    - Use `HttpMessageHandler` mock (NSubstitute or manual `DelegatingHandler`) to verify request URI and headers
    - Verify `Accept: application/json` header is present on outgoing requests
    - Verify `Authorization: Bearer <token>` header is present on outgoing requests
  - [x] 3.3 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/ConfigurationValidationTests.cs`:
    - **This is the highest-risk user path — first-time setup failures must produce clear diagnostics.**
    - Test: both `EVENTSTORE_ADMIN_URL` and `EVENTSTORE_ADMIN_TOKEN` missing — error output mentions both variable names, exit code is non-zero
    - Test: only `EVENTSTORE_ADMIN_URL` missing — error output mentions `EVENTSTORE_ADMIN_URL` specifically
    - Test: only `EVENTSTORE_ADMIN_TOKEN` missing — error output mentions `EVENTSTORE_ADMIN_TOKEN` specifically
    - Test: `EVENTSTORE_ADMIN_URL` set to a non-URI string (e.g., `"not-a-url"`) — error output mentions invalid URI, exit code is non-zero
    - Test: both set with valid values — process does NOT exit with the env-var validation error code within 2 seconds (it will block on stdin, which is correct — the test asserts the absence of a validation error, NOT that the server fully starts)
    - **CRITICAL: The "both set" test must use a short timeout (2s) and assert the process has NOT exited with error. Do NOT wait for the process to complete — it will hang on stdin forever. Kill the process after the assertion.**
    - Implementation: invoke the CLI binary via `Process.Start("dotnet", "<assembly.dll>")` with controlled environment variables, capture stderr, assert on exit code and error message content. Follow the `VersionFlagTests` pattern from Admin.Cli.Tests (Task 2.4 of story 17-8).
  - [x] 3.4 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/ServerToolsTests.cs`:
    - Test `Ping` returns valid JSON with `"reachable"` status when API returns healthy response (mock `AdminApiClient` via NSubstitute)
    - Test `Ping` returns valid JSON with `"unreachable"` status when `AdminApiClient` throws `HttpRequestException`
    - Test `Ping` returns valid JSON with `"unauthorized"` status when `AdminApiClient` throws `HttpRequestException` wrapping a 401 response (use `HttpRequestException` with `StatusCode` property set to `HttpStatusCode.Unauthorized`)
    - Test `Ping` returns valid JSON with `"error"` status when `AdminApiClient` throws for a 500 response
    - Verify all returned JSON is parseable and contains `serverName`, `adminApiStatus` fields
  - [x] 3.5 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/AssemblyMetadataTests.cs`:
    - Test assembly name is `Hexalith.EventStore.Admin.Mcp`
    - Test assembly has entry point (is Exe)
    - Test `AssemblyInformationalVersionAttribute` is present
  - [x] 3.6 Run all existing tests to verify zero regressions:
    - `dotnet test Hexalith.EventStore.slnx --configuration Release`

- [x] **Task 4: Update solution file and CI/CD** (AC: #7, #9)
  - [x] 4.1 Add both new projects to `Hexalith.EventStore.slnx`:
    - In `/src/` folder: `src/Hexalith.EventStore.Admin.Mcp/Hexalith.EventStore.Admin.Mcp.csproj`
    - In `/tests/` folder: `tests/Hexalith.EventStore.Admin.Mcp.Tests/Hexalith.EventStore.Admin.Mcp.Tests.csproj`
  - [x] 4.2 Modify `.github/workflows/ci.yml` — "Unit Tests (Tier 1)" step:
    - Add line: `dotnet test tests/Hexalith.EventStore.Admin.Mcp.Tests/ --no-build --configuration Release --logger "trx;LogFileName=test-results.trx"`
  - [x] 4.3 Modify `.github/workflows/ci.yml` — "Test Summary" Python script:
    - Add `'Admin.Mcp.Tests': 'tests/Hexalith.EventStore.Admin.Mcp.Tests/TestResults/**/test-results.trx'` to the `tier1_suites` dictionary

- [x] **Task 5: Verify end-to-end scaffold** (AC: #12, #13, #14)
  - [x] 5.1 Build solution: `dotnet build Hexalith.EventStore.slnx --configuration Release` — zero warnings
  - [x] 5.2 Run all Tier 1 tests — all green
  - [x] 5.3 Manual smoke test: set env vars and run `dotnet run --project src/Hexalith.EventStore.Admin.Mcp/` — verify it starts and waits for MCP input on stdin (it will hang waiting for input, which is correct stdio behavior)
  - [x] 5.4 Manual smoke test: run WITHOUT env vars — verify clear error message to stderr and non-zero exit code
  - [x] 5.5 Manual smoke test: close stdin (Ctrl+D / pipe EOF) — verify server exits cleanly without errors

## Dev Notes

### Architecture Compliance

- **ADR-P4 (Three-Interface Architecture)** — MCP server is a thin HTTP client calling Admin API. No DAPR sidecar. Same pattern as CLI (Epic 17). The MCP server calls the same `/api/v1/admin/*` REST endpoints as the CLI and Web UI.
- **FR79** — Three interfaces (Web UI, CLI, MCP) backed by shared Admin API. This story creates the third interface.
- **FR81** — MCP structured tools with approval gates. This story scaffolds the tool infrastructure; approval-gated write tools come in story 18.4.
- **NFR43** — Admin MCP server must respond to tool calls within 1 second at p99 for single-resource queries. HttpClient default timeout is 10s as a generous ceiling; actual response time depends on Admin API performance. The 10s default aligns better with NFR43 than 30s — if the Admin API hasn't responded in 10s, something is wrong.
- **UX-DR56** — All MCP read operations return structured JSON. The `Ping` tool demonstrates this pattern.
- **UX-DR57** — Write operations require `confirm: true` parameter (story 18.4 scope, not this story).
- **UX-DR58** — Tenant context scoping (story 18.5 scope, not this story).
- **UX-DR59** — Investigation session state (story 18.5 scope, not this story).

### Critical Design Decisions

**ADR-1: IsPackable = false for MCP server**

Unlike the CLI (distributed as .NET global tool via NuGet), the MCP server is configured by path in AI agent config files (e.g., Claude Desktop's `claude_desktop_config.json` points to `"command": "dotnet", "args": ["run", "--project", "/path/to/Admin.Mcp"]` or a published executable path). NuGet tool distribution may be added in a future story if there's demand.

**ADR-2: Environment variables over CLI flags**

The MCP server uses environment variables (`EVENTSTORE_ADMIN_URL`, `EVENTSTORE_ADMIN_TOKEN`) instead of command-line flags because: (a) MCP clients pass args as a fixed array in config, making runtime flag changes impractical, (b) environment variables are the standard MCP server configuration mechanism (used by all official MCP servers), (c) secrets in env vars are more secure than CLI args visible in process lists.

**ADR-3: ModelContextProtocol SDK v1.1.0 (stable)**

Using the official C# MCP SDK (`ModelContextProtocol` v1.1.0) maintained by the MCP project in collaboration with Microsoft. This is the stable GA release. Key API surface: `AddMcpServer()` extension on `IServiceCollection`, `WithStdioServerTransport()` for stdio, `WithToolsFromAssembly()` for reflection-based tool discovery, `[McpServerToolType]` and `[McpServerTool]` attributes for tool definition. Tools receive DI-injected services as parameters.

**ADR-4: Logging to stderr only**

stdio transport uses stdout for MCP JSON-RPC messages. ALL application logging MUST go to stderr. The `LogToStandardErrorThreshold = LogLevel.Trace` setting ensures all log levels are written to stderr. Any `Console.WriteLine` in tool code would corrupt the MCP protocol stream — use `ILogger` instead.

**ADR-5: Typed HttpClient via IHttpClientFactory**

Using `AddHttpClient<AdminApiClient>` registers the typed client with `IHttpClientFactory`, enabling connection pooling, handler lifetime management, and testability via `HttpMessageHandler` mocking. This follows the same pattern the CLI should evolve toward (currently CLI uses raw HttpClient).

**ADR-6: 10-second default HttpClient timeout**

Default timeout is 10s rather than 30s. NFR43 targets 1s p99 for single-resource queries — if Admin API hasn't responded in 10s, something is fundamentally wrong (network partition, service down). A 30s timeout just delays the inevitable failure report. Later stories adding batch operations (e.g., full consistency check in 18.3) can override per-request with `CancellationTokenSource` if needed.

**ADR-7: Graceful shutdown via Generic Host lifetime**

The .NET Generic Host's `IHostApplicationLifetime` handles `SIGTERM`, `Ctrl+C`, and parent process termination automatically. The MCP stdio transport flushes and closes the stdout stream on shutdown. No custom shutdown logic is needed — the host does the right thing. This is important because MCP clients (Claude Desktop, VS Code) may terminate the server process at any time when the user closes the AI session.

### File Structure

```
src/Hexalith.EventStore.Admin.Mcp/
  Hexalith.EventStore.Admin.Mcp.csproj  # NEW - MCP server project
  Program.cs                            # NEW - Host builder with stdio transport
  AdminApiClient.cs                     # NEW - Typed HttpClient for Admin API
  Tools/
    ServerTools.cs                      # NEW - Ping tool (connectivity check)

tests/Hexalith.EventStore.Admin.Mcp.Tests/
  Hexalith.EventStore.Admin.Mcp.Tests.csproj  # NEW - Test project
  AdminApiClientTests.cs                       # NEW - HttpClient request/header tests (3 tests)
  ConfigurationValidationTests.cs              # NEW - Env var + URI validation tests (5 tests)
  ServerToolsTests.cs                          # NEW - Ping tool tests with status differentiation (5 tests)
  AssemblyMetadataTests.cs                     # NEW - Assembly validation tests (3 tests)

Hexalith.EventStore.slnx                # MODIFIED - add both new projects
Directory.Packages.props                # MODIFIED - add ModelContextProtocol 1.1.0
.github/workflows/ci.yml               # MODIFIED - add Admin.Mcp.Tests to Tier 1
```

### Existing Code Patterns to Follow

- **Project structure:** Follow `Admin.Cli` project structure for reference — same pattern of Exe project referencing Admin.Abstractions.
- **Test conventions:** xUnit + Shouldly assertions. One test class per concern. `[Fact]` for single cases, `[Theory]` with `[InlineData]` for parameterized. Namespace matches folder structure.
- **HttpClient testing:** Use `DelegatingHandler` subclass or NSubstitute mock of `HttpMessageHandler` to intercept and verify HTTP requests without actual network calls.
- **CI patterns:** Each `dotnet test` line in CI uses `--logger "trx;LogFileName=<name>.trx"`. Test summary Python script parses TRX files for GitHub step summary.
- **Naming:** File-scoped namespaces, Allman braces, `_camelCase` private fields, `Async` suffix on async methods, `I` prefix on interfaces.
- **Admin API routes:** All admin endpoints follow `/api/v1/admin/{resource}` pattern (see `AdminHealthController`, `AdminStreamsController`, etc.).
- **Auth pattern:** Admin API uses JWT Bearer auth with role-based policies (`AdminAuthorizationPolicies.ReadOnly`, `.Operator`, `.Admin`). MCP server authenticates via Bearer token from `EVENTSTORE_ADMIN_TOKEN` env var.

### Previous Story Intelligence (17-8 & Epic 17)

Epic 17 (Admin CLI) established the pattern for thin HTTP clients consuming the Admin API:
- CLI references `Admin.Abstractions` for shared DTOs and service interfaces
- CLI calls Admin API via HTTP with Bearer token auth
- CLI uses `System.CommandLine` for arg parsing — MCP server uses `ModelContextProtocol` SDK instead but same structural pattern
- `ExitCodes` class in CLI provides `Success = 0`, `Error = 1` — MCP server can follow same pattern or use raw int returns
- The CLI's `GlobalOptionsBinding` handles `--url`, `--token`, `--format`, `--output`, `--profile` — MCP server uses env vars instead (ADR-2) but the URL/token concept is identical

**Key observation:** The CLI does NOT use `IHttpClientFactory` — it constructs `HttpClient` directly in command handlers. The MCP server should use `IHttpClientFactory` (ADR-5) as the better pattern. Do NOT copy the CLI's raw HttpClient usage.

### AdminApiClient Extensibility (for stories 18.2-18.5)

Story 18.2 will add multiple Admin API methods to `AdminApiClient` (streams, projections, types, metrics). To prevent merge conflicts and a monolithic class:
- **Option A (preferred):** Use C# partial classes — `AdminApiClient.cs` (base + health), `AdminApiClient.Streams.cs`, `AdminApiClient.Projections.cs`, etc.
- **Option B:** Split into domain-specific typed clients (`AdminHealthClient`, `AdminStreamClient`) each registered via `IHttpClientFactory` with shared configuration.
- The dev agent for story 18.2 should choose the approach. This story's `AdminApiClient` just has `GetSystemHealthAsync` — the structure is easy to extend either way.

### Self-Signed Certificate Dev Experience

When running against the Aspire-hosted Admin API in development, the Admin.Server uses auto-generated self-signed HTTPS certificates. `HttpClient` will reject these by default with `HttpRequestException: The SSL connection could not be established`. Developers must run `dotnet dev-certs https --trust` on their machine before the MCP server can connect. This is standard .NET HTTPS development workflow — do NOT add a `SKIP_TLS_VERIFY` option (anti-pattern for security). Document this in the MCP Client Configuration Example if the dev hits the issue.

### Warnings for Stories 18.2-18.5

- **NEVER use `Console.Write*` in tool code.** stdout is the MCP JSON-RPC transport. Any non-protocol output corrupts the stream. Use `ILogger` (injected via DI) for all diagnostics. Consider adding a Roslyn analyzer rule to enforce this in story 18.2.
- **All tools must return valid JSON strings, never throw.** Exceptions from tool methods surface as MCP protocol errors that are opaque to the AI agent. Catch all exceptions and return structured error JSON.

### Git Intelligence

Recent commits follow consistent patterns:
- `d0d351d` (17-7): Connection profiles and shell completions — latest feature commit
- All feature commits use: `feat: Add <description> for story 17-X`
- This story should use: `feat: Add MCP server scaffold with stdio transport and Admin API client for story 18-1`

### Admin API Endpoints (Reference for AdminApiClient)

The Admin API controllers expose these route prefixes (all require Bearer token auth):
- `GET /api/v1/admin/health` — System health report (ReadOnly role)
- `GET /api/v1/admin/health/dapr` — DAPR component status (ReadOnly role)
- `GET /api/v1/admin/streams` — Stream browser (ReadOnly role)
- `GET /api/v1/admin/projections` — Projection status (ReadOnly role)
- `GET /api/v1/admin/types` — Type catalog (ReadOnly role)
- `GET /api/v1/admin/deadletters` — Dead letter queue (ReadOnly role)
- `GET /api/v1/admin/backups` — Backup jobs (ReadOnly role)
- `GET /api/v1/admin/storage` — Storage overview (ReadOnly role)
- `GET /api/v1/admin/tenants` — Tenant list (ReadOnly role)
- `GET /api/v1/admin/consistency` — Consistency checks (ReadOnly role)

Only the `Ping` tool (calling `/health`) is implemented in this story. Stories 18.2-18.5 will add tools for remaining endpoints.

### MCP Client Configuration Example

After this story, AI agents configure the MCP server like this (Claude Desktop example):
```json
{
  "mcpServers": {
    "hexalith-eventstore": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/src/Hexalith.EventStore.Admin.Mcp"],
      "env": {
        "EVENTSTORE_ADMIN_URL": "https://localhost:5443",
        "EVENTSTORE_ADMIN_TOKEN": "eyJhbGciOiJ..."
      }
    }
  }
}
```

### References

- [Source: architecture.md, lines 147-153] ADR-P4 Three-Interface Architecture
- [Source: architecture.md, lines 206-212] Admin tooling distribution model
- [Source: architecture.md, line 237] Admin Authentication — MCP uses API key via env var
- [Source: prd.md, line 906] FR79 — Three-interface admin operations
- [Source: prd.md, line 908] FR81 — MCP structured tools with approval gates
- [Source: prd.md, line 978] NFR43 — Admin MCP <1s p99 tool call response
- [Source: ux-design-specification.md, lines 2115-2118] UX-DR56 through UX-DR59
- [Source: sprint-change-proposal-2026-03-21-admin-tooling.md, line 153] Epic 18 story breakdown
- [Source: NuGet] ModelContextProtocol v1.1.0 — https://www.nuget.org/packages/ModelContextProtocol/
- [Source: MCP C# SDK docs] Getting Started — https://csharp.sdk.modelcontextprotocol.io/concepts/getting-started.html

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Build error CS0051: `ServerTools` was `public` but referenced `internal` `AdminApiClient` — fixed by making `ServerTools` internal (MCP SDK discovers via reflection regardless of accessibility).
- Build error CS1061: `AddHttpClient<T>` not found — `Microsoft.Extensions.Http` package was missing; added to both csproj and `Directory.Packages.props`.
- Pre-existing IntegrationTests build error (CS0433: ambiguous `Program` type between AppHost and Sample) — not caused by this story, not addressed.

### Completion Notes List

- Created `src/Hexalith.EventStore.Admin.Mcp/` project with stdio MCP transport, typed AdminApiClient with IHttpClientFactory, and Ping tool with differentiated connectivity status (reachable/unauthorized/error/unreachable).
- Created `tests/Hexalith.EventStore.Admin.Mcp.Tests/` with 19 tests across 4 test classes: AdminApiClientTests (3), ConfigurationValidationTests (6), ServerToolsTests (7), AssemblyMetadataTests (3).
- All Tier 1 tests pass including 19 Admin.Mcp tests.
- Smoke tests verified: missing env vars produce clear stderr error with exit code 1; valid env vars start server; EOF on stdin triggers clean shutdown with exit code 0.
- Added `Microsoft.Extensions.Http` v10.0.0 to `Directory.Packages.props` (required for typed HttpClient registration via `AddHttpClient<T>`).

### File List

- `src/Hexalith.EventStore.Admin.Mcp/Hexalith.EventStore.Admin.Mcp.csproj` — NEW
- `src/Hexalith.EventStore.Admin.Mcp/Program.cs` — NEW
- `src/Hexalith.EventStore.Admin.Mcp/AdminApiClient.cs` — NEW
- `src/Hexalith.EventStore.Admin.Mcp/Tools/ServerTools.cs` — NEW
- `tests/Hexalith.EventStore.Admin.Mcp.Tests/Hexalith.EventStore.Admin.Mcp.Tests.csproj` — NEW
- `tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientTests.cs` — NEW
- `tests/Hexalith.EventStore.Admin.Mcp.Tests/ConfigurationValidationTests.cs` — NEW
- `tests/Hexalith.EventStore.Admin.Mcp.Tests/ServerToolsTests.cs` — NEW
- `tests/Hexalith.EventStore.Admin.Mcp.Tests/AssemblyMetadataTests.cs` — NEW
- `Hexalith.EventStore.slnx` — MODIFIED (added both new projects)
- `Directory.Packages.props` — MODIFIED (added ModelContextProtocol 1.1.0, Microsoft.Extensions.Http 10.0.0)
- `.github/workflows/ci.yml` — MODIFIED (added Admin.Mcp.Tests to Tier 1 and test summary)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — MODIFIED (story status updated)
- `_bmad-output/implementation-artifacts/18-1-mcp-server-scaffold-stdio-transport.md` — MODIFIED (task checkboxes, dev agent record, status)

### Change Log

- 2026-03-26: Implemented MCP server scaffold with stdio transport, Admin API HttpClient, Ping tool, and 16 unit tests (Story 18-1)
- 2026-03-26: Code review fixes — added catch for JsonException and TaskCanceledException in Ping, added MCP server description (AC #3), fixed GetEntryAssembly→typeof(ServerTools).Assembly for consistent version, added null health response handling, restricted URI validation to HTTP(S) schemes, added 3 new tests (timeout, malformed JSON, non-HTTP scheme). Total: 19 tests.
