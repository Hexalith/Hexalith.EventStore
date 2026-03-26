# Story 18.2: Read Tools — Stream State, Projection, Schema, Metrics

Status: ready-for-dev

Size: Large — Extends 1 existing project (Admin.Mcp), extends 1 existing test project (Admin.Mcp.Tests), adds ~10 MCP tools across 4 tool classes, extends AdminApiClient with ~15 new methods via partial classes, ~45-55 tests across 10 test classes (~8-12 hours estimated). Implements the core read-only MCP tool surface that enables AI agents to query streams, inspect projections, discover schemas, and check system metrics.

**Dependency:** Story 18-1 (MCP Server Scaffold) must be complete and in `done` status (currently in `review`). Do NOT start implementation until 18-1 review is finalized — if review surfaces changes to AdminApiClient or ServerTools, this story's dev agent must work against the post-review codebase.

## Definition of Done

- All acceptance criteria verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- All 10 MCP tools discoverable via `tools/list` and return structured JSON
- AdminApiClient extended via partial classes (no monolithic class)
- All existing Tier 1 and Tier 2 tests continue to pass with zero behavioral change

## Story

As an **AI agent (e.g., Claude) connected to the EventStore via MCP**,
I want **read-only tools to query streams, inspect aggregate state, check projection health, discover event/command/aggregate schemas, and view system metrics**,
so that **I can autonomously investigate event sourcing issues, understand the domain model, and build diagnostic context without requiring human navigation of the Web UI or CLI (FR81, UX-DR56, NFR43)**.

## Acceptance Criteria

### Stream Tools

1. **`stream-list` tool** — Calls `GET /api/v1/admin/streams` with optional `tenantId`, `domain`, and `count` (default 100) parameters. Returns structured JSON with `PagedResult<StreamSummary>` containing items, totalCount, and continuationToken. Each `StreamSummary` has: tenantId, domain, aggregateId, eventCount, lastEventSequence, lastActivityUtc, hasSnapshot, streamStatus. On error, returns structured error JSON with `adminApiStatus` field. Tool description: `"List recently active event streams, optionally filtered by tenant and domain"`.

2. **`stream-events` tool** — Calls `GET /api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/timeline` with required `tenantId`, `domain`, `aggregateId` and optional `fromSequence`, `toSequence`, `count` (default 100) parameters. Returns structured JSON with `PagedResult<TimelineEntry>` containing items, totalCount, and continuationToken. Each `TimelineEntry` has: sequenceNumber, timestamp, entryType, typeName, correlationId, userId. Tool description: `"Get the command/event/query timeline for a specific event stream"`.

3. **`stream-state` tool** — Calls `GET /api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/state?sequenceNumber={n}` with required `tenantId`, `domain`, `aggregateId`, `sequenceNumber` parameters. Returns structured JSON with the `AggregateStateSnapshot` (tenantId, domain, aggregateId, sequenceNumber, timestamp, stateJson). Tool description: `"Get the aggregate state reconstructed at a specific sequence number (point-in-time state exploration)"`.

4. **`stream-event-detail` tool** — Calls `GET /api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/events/{sequenceNumber}` with required `tenantId`, `domain`, `aggregateId`, `sequenceNumber` parameters. Returns structured JSON with the full `EventDetail` (tenantId, domain, aggregateId, sequenceNumber, eventTypeName, timestamp, correlationId, causationId, userId, payloadJson). Tool description: `"Get full details of a specific event including its payload and metadata"`.

### Projection Tools

5. **`projection-list` tool** — Calls `GET /api/v1/admin/projections` with optional `tenantId` parameter. Returns structured JSON array of `ProjectionStatus` objects (name, tenantId, status, lag, throughput, errorCount, lastProcessedPosition, lastProcessedUtc). Tool description: `"List all projections with their current status, lag, and error counts"`.

6. **`projection-detail` tool** — Calls `GET /api/v1/admin/projections/{tenantId}/{projectionName}` with required `tenantId`, `projectionName` parameters. Returns structured JSON with `ProjectionDetail` (extends ProjectionStatus with errors list, configuration, subscribedEventTypes). Tool description: `"Get detailed projection information including recent errors and configuration"`.

### Type Catalog Tools

7. **`types-list` tool** — Calls `GET /api/v1/admin/types/events`, `GET /api/v1/admin/types/commands`, and `GET /api/v1/admin/types/aggregates` with optional `domain` parameter. Returns combined structured JSON with three arrays: `eventTypes` (typeName, domain, isRejection, schemaVersion), `commandTypes` (typeName, domain, targetAggregateType), `aggregateTypes` (typeName, domain, eventCount, commandCount, hasProjections). A single tool that gives the AI agent a complete domain model overview. Tool description: `"Discover all registered event types, command types, and aggregate types in the domain model"`.

### Metrics & Health Tools

8. **`health-status` tool** — Calls `GET /api/v1/admin/health`. Returns structured JSON with `SystemHealthReport` (overallStatus, totalEventCount, eventsPerSecond, errorPercentage, daprComponents, observabilityLinks). Richer than the `Ping` tool — returns full health data rather than just connectivity status. Tool description: `"Get comprehensive system health including event throughput, error rates, and DAPR component status"`.

9. **`health-dapr` tool** — Calls `GET /api/v1/admin/health/dapr`. Returns structured JSON array of `DaprComponentHealth` objects (componentName, componentType, status, lastCheckUtc). Tool description: `"Get DAPR infrastructure component health status"`.

10. **`storage-overview` tool** — Calls `GET /api/v1/admin/storage/overview` with optional `tenantId` parameter. Returns structured JSON with `StorageOverview` (totalEventCount, totalSizeBytes, tenantBreakdown, totalStreamCount). Tool description: `"Get storage usage overview including event counts, sizes, and per-tenant breakdown"`.

### Cross-Cutting

11. **All tools return structured JSON** — Every tool returns a valid JSON string regardless of success or failure. Success responses include the data directly. Error responses include `{ "error": true, "adminApiStatus": "<status>", "message": "<detail>" }` with status differentiation: `"unauthorized"` (401/403), `"not-found"` (404), `"server-error"` (5xx with status code), `"unreachable"` (connection failure), `"timeout"` (HttpClient 10s timeout — throws `TaskCanceledException`). This pattern extends the existing `Ping` tool's error handling.

12. **All tools are decorated with `[Description]`** — Each `[McpServerTool]` method has a `[Description("...")]` attribute providing a clear, concise description of what the tool does and when to use it.

13. **AdminApiClient extended via partial classes** — New API methods are organized into partial class files: `AdminApiClient.Streams.cs`, `AdminApiClient.Projections.cs`, `AdminApiClient.Types.cs`, `AdminApiClient.Storage.cs`. The base `AdminApiClient.cs` retains only `GetSystemHealthAsync` and shared helper methods.

14. **No regressions** — All existing Tier 1 and Tier 2 tests continue to pass with zero behavioral change.

## Tasks / Subtasks

- [ ] **Task 1: Extend AdminApiClient with partial classes** (AC: #13)
  - [ ] 1.1 Refactor `AdminApiClient.cs` to be `partial class`. Add a private helper method for error-safe HTTP GET:
    ```csharp
    internal async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
    ```
    This method calls `HttpClient.GetFromJsonAsync<T>(path, cancellationToken)` and is reused by all partial class files. This avoids duplicating HTTP call + deserialization logic across every method.
    **IMPORTANT**: `GetFromJsonAsync<T>` returns `null` on 204 No Content or empty response bodies. The helper must handle this gracefully — tools should treat `null` as an empty result set (e.g., empty array `[]`), NOT as an error. Add a companion helper for list endpoints:
    ```csharp
    internal async Task<IReadOnlyList<T>> GetListAsync<T>(string path, CancellationToken cancellationToken)
    ```
    This calls `GetAsync<IReadOnlyList<T>>` and returns `Array.Empty<T>()` when the result is `null`.
    **Single-entity endpoints** (`stream-state`, `stream-event-detail`, `projection-detail`): When `GetAsync<T>` returns `null` for a single entity, tools must return a `"not-found"` error JSON via `ToolHelper.SerializeError("not-found", "...")` — not serialize `null` as JSON. The Admin API returns 404 for missing entities, which `GetFromJsonAsync` may surface as an `HttpRequestException` with `StatusCode = 404` OR as `null` depending on the response body. Handle both paths.
  - [ ] 1.2 Create `AdminApiClient.Streams.cs` — partial class with stream query methods:
    - `GetRecentlyActiveStreamsAsync(string? tenantId, string? domain, int count, CancellationToken)` → calls `GET /api/v1/admin/streams?count={count}&tenantId={tenantId}&domain={domain}`
    - `GetStreamTimelineAsync(string tenantId, string domain, string aggregateId, long? fromSequence, long? toSequence, int count, CancellationToken)` → calls `GET /api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/timeline?fromSequence={from}&toSequence={to}&count={count}`
    - `GetAggregateStateAsync(string tenantId, string domain, string aggregateId, long sequenceNumber, CancellationToken)` → calls `GET /api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/state?sequenceNumber={n}`
    - `GetEventDetailAsync(string tenantId, string domain, string aggregateId, long sequenceNumber, CancellationToken)` → calls `GET /api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/events/{sequenceNumber}`
  - [ ] 1.3 Create `AdminApiClient.Projections.cs` — partial class with projection query methods:
    - `ListProjectionsAsync(string? tenantId, CancellationToken)` → calls `GET /api/v1/admin/projections?tenantId={tenantId}`
    - `GetProjectionDetailAsync(string tenantId, string projectionName, CancellationToken)` → calls `GET /api/v1/admin/projections/{tenantId}/{projectionName}`
  - [ ] 1.4 Create `AdminApiClient.Types.cs` — partial class with type catalog methods:
    - `ListEventTypesAsync(string? domain, CancellationToken)` → calls `GET /api/v1/admin/types/events?domain={domain}`
    - `ListCommandTypesAsync(string? domain, CancellationToken)` → calls `GET /api/v1/admin/types/commands?domain={domain}`
    - `ListAggregateTypesAsync(string? domain, CancellationToken)` → calls `GET /api/v1/admin/types/aggregates?domain={domain}`
  - [ ] 1.5 Create `AdminApiClient.Storage.cs` — partial class with storage/metrics methods:
    - `GetDaprComponentStatusAsync(CancellationToken)` → calls `GET /api/v1/admin/health/dapr`
    - `GetStorageOverviewAsync(string? tenantId, CancellationToken)` → calls `GET /api/v1/admin/storage/overview?tenantId={tenantId}`
  - [ ] 1.6 All methods use `System.Net.Http.Json` extensions (`GetFromJsonAsync<T>`). All methods are `async Task<T?>` with `CancellationToken`. All path segments are URI-encoded via `Uri.EscapeDataString()`.

- [ ] **Task 2: Create shared error-handling helper** (AC: #11) — **Must be completed BEFORE Tasks 3-6** so tool classes can use ToolHelper from the start.
  - [ ] 2.1 Create `src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs` — `internal static` helper class with:
    - `static JsonSerializerOptions JsonOptions` — shared serializer options (camelCase, indented, enum-as-string)
    - `static string SerializeResult<T>(T data)` — serializes success response to JSON
    - `static string SerializeError(string adminApiStatus, string message)` — serializes error response with standard shape `{ "error": true, "adminApiStatus": "...", "message": "..." }`
    - `static string HandleHttpException(HttpRequestException ex)` — categorizes HttpRequestException into `"unauthorized"` (401/403), `"not-found"` (404), `"server-error"` (5xx), or `"unreachable"` (no status code / connection error) and returns error JSON
    - `static string HandleException(Exception ex)` — entry point that catches BOTH `HttpRequestException` (delegates to `HandleHttpException`) AND `TaskCanceledException` (returns `"timeout"` status with message `"Request timed out after 10 seconds"`). The 10-second HttpClient timeout throws `TaskCanceledException`, NOT `HttpRequestException` — this is a common .NET gotcha. All tool catch blocks should use `HandleException` as the outer handler.
  - [ ] 2.2 Refactor the existing `ServerTools.Ping` method to use `ToolHelper` for error serialization (keeping Ping's richer response structure for the connectivity-specific fields like `serverName` and `serverVersion`). Do NOT over-abstract the success path — Ping's custom response shape is intentional and should remain bespoke. Also add `[McpServerTool(Name = "ping")]` to the existing Ping method for naming consistency with all new tools.

- [ ] **Task 3: Create Stream MCP tools** (AC: #1, #2, #3, #4, #11, #12)
  - [ ] 2.1 Create `src/Hexalith.EventStore.Admin.Mcp/Tools/StreamTools.cs`:
    ```csharp
    [McpServerToolType]
    public static class StreamTools
    {
        [McpServerTool(Name = "stream-list")]
        [Description("List recently active event streams, optionally filtered by tenant and domain")]
        public static async Task<string> ListStreams(
            AdminApiClient adminApiClient,
            [Description("Filter by tenant ID")] string? tenantId = null,
            [Description("Filter by domain")] string? domain = null,
            [Description("Max streams to return (default 100)")] int count = 100,
            CancellationToken cancellationToken = default) { ... }

        [McpServerTool(Name = "stream-events")]
        [Description("Get the command/event/query timeline for a specific event stream")]
        public static async Task<string> GetStreamEvents(
            AdminApiClient adminApiClient,
            [Description("Tenant ID")] string tenantId,
            [Description("Domain name")] string domain,
            [Description("Aggregate ID")] string aggregateId,
            [Description("Start from sequence number")] long? fromSequence = null,
            [Description("End at sequence number")] long? toSequence = null,
            [Description("Max entries to return (default 100)")] int count = 100,
            CancellationToken cancellationToken = default) { ... }

        [McpServerTool(Name = "stream-state")]
        [Description("Get the aggregate state reconstructed at a specific sequence number (point-in-time state exploration)")]
        public static async Task<string> GetStreamState(
            AdminApiClient adminApiClient,
            [Description("Tenant ID")] string tenantId,
            [Description("Domain name")] string domain,
            [Description("Aggregate ID")] string aggregateId,
            [Description("Sequence number to reconstruct state at")] long sequenceNumber,
            CancellationToken cancellationToken = default) { ... }

        [McpServerTool(Name = "stream-event-detail")]
        [Description("Get full details of a specific event including its payload and metadata")]
        public static async Task<string> GetEventDetail(
            AdminApiClient adminApiClient,
            [Description("Tenant ID")] string tenantId,
            [Description("Domain name")] string domain,
            [Description("Aggregate ID")] string aggregateId,
            [Description("Event sequence number")] long sequenceNumber,
            CancellationToken cancellationToken = default) { ... }
    }
    ```
  - [ ] 2.2 Each method follows the same error-handling pattern as `ServerTools.Ping`:
    - Try calling `AdminApiClient` method
    - Serialize result to JSON via `JsonSerializer.Serialize(data, JsonOptions)`
    - Wrap in try/catch with `catch (Exception ex) { return ToolHelper.HandleException(ex); }`
    - This catches both `HttpRequestException` (with status code differentiation) and `TaskCanceledException` (timeout)
    - For single-entity tools (`stream-state`, `stream-event-detail`): check for null result BEFORE serializing and return `ToolHelper.SerializeError("not-found", "...")` instead
    - NEVER throw — always return valid JSON string
  - [ ] 2.3 Create a shared `JsonOptions` static property (or reuse if already exists) with `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` and `WriteIndented = true` for readable AI agent output.

- [ ] **Task 4: Create Projection MCP tools** (AC: #5, #6, #11, #12)
  - [ ] 3.1 Create `src/Hexalith.EventStore.Admin.Mcp/Tools/ProjectionTools.cs`:
    ```csharp
    [McpServerToolType]
    public static class ProjectionTools
    {
        [McpServerTool(Name = "projection-list")]
        [Description("List all projections with their current status, lag, and error counts")]
        public static async Task<string> ListProjections(
            AdminApiClient adminApiClient,
            [Description("Filter by tenant ID")] string? tenantId = null,
            CancellationToken cancellationToken = default) { ... }

        [McpServerTool(Name = "projection-detail")]
        [Description("Get detailed projection information including recent errors and configuration")]
        public static async Task<string> GetProjectionDetail(
            AdminApiClient adminApiClient,
            [Description("Tenant ID")] string tenantId,
            [Description("Projection name")] string projectionName,
            CancellationToken cancellationToken = default) { ... }
    }
    ```

- [ ] **Task 5: Create Type Catalog MCP tool** (AC: #7, #11, #12)
  - [ ] 4.1 Create `src/Hexalith.EventStore.Admin.Mcp/Tools/TypeCatalogTools.cs`:
    ```csharp
    [McpServerToolType]
    public static class TypeCatalogTools
    {
        [McpServerTool(Name = "types-list")]
        [Description("Discover all registered event types, command types, and aggregate types in the domain model")]
        public static async Task<string> ListTypes(
            AdminApiClient adminApiClient,
            [Description("Filter by domain")] string? domain = null,
            CancellationToken cancellationToken = default) { ... }
    }
    ```
  - [ ] 5.2 The `types-list` tool makes three parallel API calls via `Task.WhenAll` for event types, command types, and aggregate types, then combines them into a single JSON response. **Implementation hint:** Declare three separately-typed task variables before awaiting:
    ```csharp
    var eventTypesTask = adminApiClient.ListEventTypesAsync(domain, cancellationToken);
    var commandTypesTask = adminApiClient.ListCommandTypesAsync(domain, cancellationToken);
    var aggregateTypesTask = adminApiClient.ListAggregateTypesAsync(domain, cancellationToken);
    await Task.WhenAll(eventTypesTask, commandTypesTask, aggregateTypesTask);
    ```
    Then access `.Result` on each task individually, wrapping each in a try/catch for partial failure. Combines into a single JSON response:
    ```json
    {
      "eventTypes": [...],
      "commandTypes": [...],
      "aggregateTypes": [...]
    }
    ```
  - [ ] 5.3 If one of the three calls fails, include the error for that category and return whatever succeeded. Do not fail the entire tool.

- [ ] **Task 6: Create Metrics & Health MCP tools** (AC: #8, #9, #10, #11, #12)
  - [ ] 5.1 Add tools to the existing `src/Hexalith.EventStore.Admin.Mcp/Tools/ServerTools.cs` (which already has `Ping`):
    ```csharp
    [McpServerTool(Name = "health-status")]
    [Description("Get comprehensive system health including event throughput, error rates, and DAPR component status")]
    public static async Task<string> GetHealthStatus(
        AdminApiClient adminApiClient,
        CancellationToken cancellationToken = default) { ... }

    [McpServerTool(Name = "health-dapr")]
    [Description("Get DAPR infrastructure component health status")]
    public static async Task<string> GetDaprHealth(
        AdminApiClient adminApiClient,
        CancellationToken cancellationToken = default) { ... }
    ```
  - [ ] 5.2 Create `src/Hexalith.EventStore.Admin.Mcp/Tools/StorageTools.cs`:
    ```csharp
    [McpServerToolType]
    public static class StorageTools
    {
        [McpServerTool(Name = "storage-overview")]
        [Description("Get storage usage overview including event counts, sizes, and per-tenant breakdown")]
        public static async Task<string> GetStorageOverview(
            AdminApiClient adminApiClient,
            [Description("Filter by tenant ID")] string? tenantId = null,
            CancellationToken cancellationToken = default) { ... }
    }
    ```

- [ ] **Task 8: Create tests for AdminApiClient partial classes** (AC: #13)
  - [ ] 7.1 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientStreamTests.cs`:
    - Test `GetRecentlyActiveStreamsAsync` sends GET to correct path with query parameters
    - Test `GetStreamTimelineAsync` sends GET to correct path with URI-encoded segments
    - Test `GetAggregateStateAsync` sends GET to correct path with sequenceNumber query param
    - Test `GetEventDetailAsync` sends GET to correct path with sequence in URL path
    - Test query parameters are omitted when null (no `&tenantId=&domain=` in URL)
    - **`[Theory]` with edge-case aggregate IDs** for URI encoding: use `[InlineData]` with values like `"simple-id"`, `"id/with/slashes"`, `"id with spaces"`, `"id+plus"`, `"unicod\u00e9"`, and a 256-char long ID. Verify `Uri.EscapeDataString()` produces correct path segments for each.
  - [ ] 7.2 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientProjectionTests.cs`:
    - Test `ListProjectionsAsync` sends GET to correct path, with and without tenantId
    - Test `GetProjectionDetailAsync` sends GET to correct path with URI-encoded projection name
  - [ ] 7.3 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientTypeTests.cs`:
    - Test `ListEventTypesAsync` sends GET to `/api/v1/admin/types/events`
    - Test `ListCommandTypesAsync` sends GET to `/api/v1/admin/types/commands`
    - Test `ListAggregateTypesAsync` sends GET to `/api/v1/admin/types/aggregates`
    - Test domain filter query parameter when provided
  - [ ] 7.4 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientStorageTests.cs`:
    - Test `GetDaprComponentStatusAsync` sends GET to `/api/v1/admin/health/dapr`
    - Test `GetStorageOverviewAsync` sends GET to `/api/v1/admin/storage/overview`

- [ ] **Task 9: Create tests for MCP tools** (AC: #1-#12)
  - [ ] 8.1 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/StreamToolsTests.cs`:
    - Test `ListStreams` returns valid JSON with stream data on success
    - Test `ListStreams` returns error JSON with `"unreachable"` on HttpRequestException
    - Test `ListStreams` returns error JSON with `"unauthorized"` on 401
    - Test `GetStreamEvents` returns valid JSON with timeline entries
    - Test `GetStreamState` returns valid JSON with aggregate state
    - Test `GetEventDetail` returns valid JSON with event detail
    - Test all tools return parseable JSON (deserializable to JsonDocument)
  - [ ] 8.2 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/ProjectionToolsTests.cs`:
    - Test `ListProjections` returns valid JSON with projection list
    - Test `ListProjections` returns error JSON on failure
    - Test `GetProjectionDetail` returns valid JSON with projection detail
    - Test `GetProjectionDetail` returns `"not-found"` error on 404
  - [ ] 8.3 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/TypeCatalogToolsTests.cs`:
    - Test `ListTypes` returns combined JSON with eventTypes, commandTypes, aggregateTypes arrays
    - Test `ListTypes` handles partial failure (one API call fails, others succeed) — **verify that successful categories' data is still present in the response AND the failed category includes an error entry with the failure reason**
    - Test `ListTypes` returns error JSON when all calls fail
    - Test domain filter parameter is passed through
  - [ ] 8.4 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/StorageToolsTests.cs`:
    - Test `GetStorageOverview` returns valid JSON with storage data
    - Test `GetStorageOverview` returns error JSON on failure
    - Test tenantId filter parameter
  - [ ] 8.5 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/HealthToolsTests.cs`:
    - Test `GetHealthStatus` returns valid JSON with full health report
    - Test `GetDaprHealth` returns valid JSON with DAPR component list
    - Test error handling for both tools
  - [ ] 8.6 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/ToolHelperTests.cs` — **dedicated test class for `ToolHelper.HandleHttpException`**:
    - `[Theory]` with all HTTP status codes: 401, 403 → `"unauthorized"`; 404 → `"not-found"`; 500, 502, 503 → `"server-error"` with status code in message; null StatusCode → `"unreachable"`; `TaskCanceledException` → `"timeout"`
    - Test `SerializeResult<T>` produces valid camelCase JSON
    - Test `SerializeError` produces standard error shape with all required fields
    - **This is higher-value than repeating error tests in every tool class.** Individual tool tests can focus on success paths and tool-specific behavior; defer error-path coverage to this centralized test class.
  - [ ] 8.7 All tool tests use `MockHttpMessageHandler` to provide canned JSON responses (mock the HttpMessageHandler, not the client itself, since AdminApiClient is a concrete class). Consider extracting the existing `MockHttpMessageHandler` inner class from `AdminApiClientTests.cs` into a shared `TestHelpers/MockHttpMessageHandler.cs` file if the duplication across test classes becomes unwieldy. The CLI tests' `QueuedMockHttpMessageHandler` pattern (queuing multiple responses) may be useful for the `types-list` partial failure tests that require 3 sequential HTTP responses.

- [ ] **Task 10: Verify end-to-end** (AC: #14)
  - [ ] 9.1 Build solution: `dotnet build Hexalith.EventStore.slnx --configuration Release` — zero warnings
  - [ ] 9.2 Run all Tier 1 tests — all green
  - [ ] 9.3 Verify all 10 new tools appear in `tools/list` response (the MCP SDK's `WithToolsFromAssembly()` discovers all `[McpServerToolType]` classes automatically)

## Dev Notes

### Architecture Compliance

- **ADR-P4 (Three-Interface Architecture)** — MCP server is a thin HTTP client calling Admin API. No DAPR sidecar. Same pattern as CLI (Epic 17) and story 18-1.
- **FR81** — MCP structured tools with approval gates. This story implements the read-only tool surface. Write tools (approval-gated) are story 18.4 scope.
- **NFR43** — All read tools must respond within 1s at p99 for single-resource queries. HttpClient timeout is 10s (ceiling). The `types-list` tool makes 3 parallel API calls but each individual call should complete in <500ms.
- **UX-DR56** — All read operations return structured JSON. No HTML, no human-formatted output. All tool responses are machine-parseable.
- **UX-DR57** — Approval-gated writes — NOT this story's scope (story 18.4).
- **UX-DR58** — Tenant context scoping — NOT this story's scope (story 18.5). Tools in this story accept explicit `tenantId` parameters.
- **UX-DR59** — Investigation session state — NOT this story's scope (story 18.5).

### Critical Design Decisions

**ADR-1: Partial classes for AdminApiClient**

AdminApiClient uses C# partial classes to organize methods by domain:
- `AdminApiClient.cs` — Base class with constructor, `GetSystemHealthAsync`, and shared `GetAsync<T>` helper
- `AdminApiClient.Streams.cs` — Stream query methods
- `AdminApiClient.Projections.cs` — Projection query methods
- `AdminApiClient.Types.cs` — Type catalog methods
- `AdminApiClient.Storage.cs` — Storage and DAPR health methods

This prevents a monolithic 500+ line class and avoids merge conflicts in stories 18.3-18.5. The existing `AdminApiClient.cs` already has `GetSystemHealthAsync` — do NOT move it; just make the class `partial`.

**ADR-2: One tool class per domain, not one tool class per tool**

Tools are grouped by domain (`StreamTools`, `ProjectionTools`, `TypeCatalogTools`, `StorageTools`) rather than having a single monolithic `ReadTools` class. Health tools (`health-status`, `health-dapr`) are added to the existing `ServerTools` class since they are server-level diagnostics. The MCP SDK's `[McpServerToolType]` attribute works at the class level — `WithToolsFromAssembly()` discovers all classes.

**ADR-3: Shared ToolHelper for error-handling consistency**

All tools delegate error JSON serialization to `ToolHelper.HandleHttpException()`. This ensures consistent error response shape across all tools and prevents copy-paste drift. The error JSON shape is:
```json
{ "error": true, "adminApiStatus": "unauthorized|not-found|server-error|unreachable|timeout", "message": "..." }
```

**ADR-4: types-list as a single combined tool**

Rather than three separate tools (`types-events`, `types-commands`, `types-aggregates`), a single `types-list` tool makes three parallel API calls and returns combined results. Rationale: AI agents typically want the full domain model overview in one call. Three separate tools would require 3 tool calls for the same information, wasting tokens and latency. If partial failure occurs, the tool returns whatever succeeded plus error details for what failed.

**ADR-5: Tool naming convention**

Tool names use kebab-case: `{domain}-{action}` pattern. Examples: `stream-list`, `stream-events`, `stream-state`, `projection-list`, `projection-detail`, `types-list`, `health-status`, `health-dapr`, `storage-overview`. This matches MCP community conventions and is easily parseable by AI agents.

**ADR-6: health-status vs Ping**

The existing `Ping` tool checks connectivity and returns a minimal `{ serverName, serverVersion, adminApiStatus }` response. The new `health-status` tool returns the full `SystemHealthReport` with event throughput, error rates, and DAPR component status. Both are useful: `Ping` for quick connectivity validation, `health-status` for comprehensive diagnostics. They coexist.

### File Structure

```
src/Hexalith.EventStore.Admin.Mcp/
  AdminApiClient.cs                     # MODIFIED - add 'partial', add GetAsync<T> helper
  AdminApiClient.Streams.cs             # NEW - stream query methods (4 methods)
  AdminApiClient.Projections.cs         # NEW - projection query methods (2 methods)
  AdminApiClient.Types.cs               # NEW - type catalog methods (3 methods)
  AdminApiClient.Storage.cs             # NEW - storage + DAPR health methods (2 methods)
  Tools/
    ServerTools.cs                      # MODIFIED - add health-status and health-dapr tools
    StreamTools.cs                      # NEW - 4 stream read tools
    ProjectionTools.cs                  # NEW - 2 projection read tools
    TypeCatalogTools.cs                 # NEW - 1 combined types tool
    StorageTools.cs                     # NEW - 1 storage overview tool
    ToolHelper.cs                       # NEW - shared JSON serialization + error handling

tests/Hexalith.EventStore.Admin.Mcp.Tests/
  AdminApiClientStreamTests.cs          # NEW - stream client method tests + URI edge cases
  AdminApiClientProjectionTests.cs      # NEW - projection client method tests
  AdminApiClientTypeTests.cs            # NEW - type catalog client method tests
  AdminApiClientStorageTests.cs         # NEW - storage/DAPR client method tests
  StreamToolsTests.cs                   # NEW - stream tool tests (success paths)
  ProjectionToolsTests.cs              # NEW - projection tool tests (success paths)
  TypeCatalogToolsTests.cs             # NEW - type catalog tool tests + partial failure
  StorageToolsTests.cs                  # NEW - storage tool tests (success paths)
  HealthToolsTests.cs                   # NEW - health-status and health-dapr tool tests
  ToolHelperTests.cs                    # NEW - centralized error-handling tests (all status codes)
```

### Existing Code Patterns to Follow

- **AdminApiClient.cs**: Currently has `GetSystemHealthAsync` calling `GetFromJsonAsync<SystemHealthReport>`. New methods follow the same pattern. Make the class `partial` and keep all existing code in place.
- **ServerTools.cs**: Currently has `Ping` as a `[McpServerTool]` static method. New health tools follow the same static method pattern with DI-injected `AdminApiClient`. Error handling catches `HttpRequestException` and returns structured JSON.
- **Test patterns**: Existing `AdminApiClientTests.cs` uses a `MockHttpMessageHandler` inner class to capture and verify HTTP requests. Reuse this same pattern (or extract to a shared test helper if the inner class becomes unwieldy). All tests use xUnit `[Fact]`/`[Theory]`, Shouldly assertions.
- **JSON serialization**: Use `System.Text.Json` with `JsonNamingPolicy.CamelCase`. The Admin.Abstractions DTOs use standard C# PascalCase properties — `JsonSerializerOptions` handles the casing conversion.
- **URI encoding**: Path segments must be URI-encoded (`Uri.EscapeDataString()`) to handle aggregate IDs or projection names containing special characters.
- **Namespace convention**: `Hexalith.EventStore.Admin.Mcp` for root classes, `Hexalith.EventStore.Admin.Mcp.Tools` for tool classes. File-scoped namespaces.

### Previous Story Intelligence (18-1)

Story 18-1 established:
- `AdminApiClient` as `internal sealed class` — change to `internal sealed partial class`
- `ServerTools` as `[McpServerToolType] public class` with static `Ping` method
- `Program.cs` with `WithToolsFromAssembly()` — automatically discovers new `[McpServerToolType]` classes, no registration needed
- Error handling pattern in `Ping`: catches `HttpRequestException`, checks `ex.StatusCode` for 401/403 differentiation, returns structured JSON. This pattern must be generalized to `ToolHelper` and reused.
- `MockHttpMessageHandler` inner class in `AdminApiClientTests.cs` for HTTP mocking

**Key observation from 18-1**: The `Ping` tool creates its response JSON manually via anonymous object + `JsonSerializer.Serialize()`. This works but should be standardized via `ToolHelper` for consistency across 10 tools.

### Admin API Endpoints Used by This Story

| Tool | HTTP Method | Endpoint | Parameters |
|------|-------------|----------|-----------|
| stream-list | GET | /api/v1/admin/streams | ?tenantId, ?domain, ?count |
| stream-events | GET | /api/v1/admin/streams/{t}/{d}/{a}/timeline | ?fromSequence, ?toSequence, ?count |
| stream-state | GET | /api/v1/admin/streams/{t}/{d}/{a}/state | ?sequenceNumber |
| stream-event-detail | GET | /api/v1/admin/streams/{t}/{d}/{a}/events/{seq} | (path) |
| projection-list | GET | /api/v1/admin/projections | ?tenantId |
| projection-detail | GET | /api/v1/admin/projections/{t}/{name} | (path) |
| types-list | GET | /api/v1/admin/types/events | ?domain |
| types-list | GET | /api/v1/admin/types/commands | ?domain |
| types-list | GET | /api/v1/admin/types/aggregates | ?domain |
| health-status | GET | /api/v1/admin/health | (none) |
| health-dapr | GET | /api/v1/admin/health/dapr | (none) |
| storage-overview | GET | /api/v1/admin/storage/overview | ?tenantId |

### Admin.Abstractions DTOs Referenced

**Stream models:** `StreamSummary`, `TimelineEntry`, `AggregateStateSnapshot`, `EventDetail`, `PagedResult<T>`
**Projection models:** `ProjectionStatus`, `ProjectionDetail`
**Type catalog models:** `EventTypeInfo`, `CommandTypeInfo`, `AggregateTypeInfo`
**Health models:** `SystemHealthReport`, `DaprComponentHealth`
**Storage models:** `StorageOverview`

All DTOs are in `Hexalith.EventStore.Admin.Abstractions.Models.*` — the MCP project already references Admin.Abstractions via ProjectReference.

### Warnings

- **NEVER use `Console.Write*` in tool code.** stdout is the MCP JSON-RPC transport. Any non-protocol output corrupts the stream. Use `ILogger` (injected via DI) for diagnostics.
- **All tools must return valid JSON strings, never throw.** Exceptions from tool methods surface as MCP protocol errors that are opaque to the AI agent. Catch all exceptions and return structured error JSON.
- **URI-encode path segments.** Aggregate IDs may contain characters that need encoding (e.g., `/`, `+`). Always use `Uri.EscapeDataString()` for path segments interpolated into URLs.
- **Do NOT add `Console.Error.WriteLine` in tools either.** stderr goes to the MCP client's log. Use `ILogger` which is already configured to write to stderr by the host. If you need logging in a tool, inject `ILogger<T>` via the tool method parameters (MCP SDK supports DI parameter injection).

### Git Intelligence

Recent commits follow consistent patterns:
- `049dc7b` (17-8): dotnet tool packaging — latest feature commit
- All feature commits use: `feat: Add <description> for story 17-X`
- This story should use: `feat: Add MCP read tools for streams, projections, types, and metrics for story 18-2`
- Branch naming: `feat/story-18-2-read-tools-stream-state-projection-schema-metrics`

### References

- [Source: architecture.md, lines 147-153] ADR-P4 Three-Interface Architecture
- [Source: architecture.md, lines 204-212] Admin component distribution model
- [Source: architecture.md, line 237] Admin Authentication — MCP uses API key via env var
- [Source: prd.md, line 908] FR81 — MCP structured tools with approval gates
- [Source: prd.md, line 978] NFR43 — Admin MCP <1s p99 tool call response
- [Source: ux-design-specification.md, lines 2115-2118] UX-DR56 through UX-DR59
- [Source: sprint-change-proposal-2026-03-21-admin-tooling.md, line 154] Story 18.2 description
- [Source: 18-1-mcp-server-scaffold-stdio-transport.md] Previous story — scaffold patterns, AdminApiClient, ServerTools
- [Source: Admin.Abstractions/Services/] IStreamQueryService, IProjectionQueryService, ITypeCatalogService, IHealthQueryService, IStorageQueryService
- [Source: Admin.Server/Controllers/] AdminStreamsController, AdminProjectionsController, AdminTypeCatalogController, AdminHealthController, AdminStorageController

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
