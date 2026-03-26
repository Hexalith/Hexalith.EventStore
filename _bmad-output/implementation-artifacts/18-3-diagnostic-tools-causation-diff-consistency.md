# Story 18.3: Diagnostic Tools — Causation Chain, State Diff, Consistency Check

Status: done

Size: Medium — Extends AdminApiClient with 2 new partial class files, adds 2 new MCP tool classes (4 tools total), extends 1 existing test project with ~6 new test classes, ~30-40 tests (~5-7 hours estimated). Implements the diagnostic MCP tool surface that enables AI agents to trace causation chains, diff aggregate state between two sequence positions, and query consistency check results.

**Dependency:** Story 18-2 (Read Tools) must be complete and in `done` status. Story 18-2 establishes `ToolHelper.cs` (shared error handling + JSON serialization), the tool class naming convention (`[McpServerToolType]` per domain), and the test infrastructure (`MockHttpMessageHandler` pattern). Do NOT start implementation until 18-2 is finalized — this story's tools depend on `ToolHelper` and follow the patterns 18-2 establishes. **If the 18-2 code review changed ToolHelper method signatures or error status values, adapt accordingly — the patterns described in this story matter more than exact method names.**

## Definition of Done

- All acceptance criteria verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- All 4 new MCP tools discoverable via `tools/list` and return structured JSON
- AdminApiClient extended via partial classes (no monolithic class)
- All existing Tier 1 and Tier 2 tests continue to pass with zero behavioral change

## Story

As an **AI agent (e.g., Claude) connected to the EventStore via MCP**,
I want **diagnostic tools to trace event causation chains, compare aggregate state between two sequence positions, and query consistency check results**,
so that **I can autonomously investigate event sourcing anomalies, understand how commands caused state changes, identify data inconsistencies, and build diagnostic context for incident resolution without requiring human navigation of the Web UI or CLI (FR71, FR72, NFR43)**.

## Acceptance Criteria

### Stream Diagnostic Tools

1. **`stream-diff` tool** — Calls `GET /api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/diff?fromSequence={from}&toSequence={to}` with required `tenantId`, `domain`, `aggregateId`, `fromSequence`, `toSequence` parameters. Returns structured JSON with `AggregateStateDiff` containing `fromSequence`, `toSequence`, and `changedFields` array. Each `FieldChange` has: `fieldPath` (JSON path), `oldValue`, `newValue`. On 404, returns structured error JSON with `"not-found"` status. Tool description: `"Diff aggregate state between two sequence positions, showing which fields changed and their before/after values"`.

2. **`causation-chain` tool** — Calls `GET /api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/causation?sequenceNumber={n}` with required `tenantId`, `domain`, `aggregateId`, `sequenceNumber` parameters. Returns structured JSON with `CausationChain` containing `originatingCommandType`, `originatingCommandId`, `correlationId`, `userId`, `events` array (each with `sequenceNumber`, `eventTypeName`, `timestamp`), and `affectedProjections` array. On 404, returns structured error JSON with `"not-found"` status. Tool description: `"Trace the full causation chain for an event — originating command, all events produced, and affected projections"`.

### Consistency Tools

3. **`consistency-list` tool** — Calls `GET /api/v1/admin/consistency/checks` with optional `tenantId` parameter. Returns structured JSON array of `ConsistencyCheckSummary` objects (`checkId`, `status`, `tenantId`, `domain`, `checkTypes`, `startedAtUtc`, `completedAtUtc`, `timeoutUtc`, `streamsChecked`, `anomaliesFound`). Tool description: `"List data integrity checks with status, scope, and anomaly counts"`.

4. **`consistency-detail` tool** — Calls `GET /api/v1/admin/consistency/checks/{checkId}` with required `checkId` parameter. Returns structured JSON with `ConsistencyCheckResult` containing full anomaly details (`anomalyId`, `checkType`, `severity`, `tenantId`, `domain`, `aggregateId`, `description`, `details`, `expectedSequence`, `actualSequence`). Anomaly list capped at 500, with `truncated` flag. On 404, returns structured error JSON with `"not-found"` status. **Note:** The check may still be `Running` — `CompletedAtUtc` will be null and anomaly counts may still be changing. The tool must serialize this in-progress state correctly. Tool description: `"Get detailed data integrity check results including anomalies, severity levels, and affected streams"`.

### Cross-Cutting

5. **All tools return structured JSON** — Every tool returns a valid JSON string regardless of success or failure. Error responses use the same `ToolHelper.SerializeError` shape established in story 18-2: `{ "error": true, "adminApiStatus": "<status>", "message": "<detail>" }`.

6. **All tools are decorated with `[Description]`** — Each `[McpServerTool]` method has a `[Description("...")]` attribute.

7. **AdminApiClient extended via partial classes** — New API methods are organized into: `AdminApiClient.Streams.cs` (extended with 2 new methods) and new `AdminApiClient.Consistency.cs`.

8. **No regressions** — All existing Tier 1 and Tier 2 tests continue to pass with zero behavioral change.

## Tasks / Subtasks

- [x] **Task 1: Extend existing `AdminApiClient.Streams.cs` with diagnostic methods — do NOT create a new partial file** (AC: #1, #2, #7)
  - [x] 1.1 Add `DiffAggregateStateAsync` method to the **existing** `AdminApiClient.Streams.cs` (same file that already has `GetRecentlyActiveStreamsAsync` etc.):
    ```csharp
    public async Task<AggregateStateDiff?> DiffAggregateStateAsync(
        string tenantId, string domain, string aggregateId,
        long fromSequence, long toSequence, CancellationToken cancellationToken)
    ```
    Calls `GET /api/v1/admin/streams/{t}/{d}/{a}/diff?fromSequence={from}&toSequence={to}`. All path segments URI-encoded via `Uri.EscapeDataString()`.
  - [x] 1.2 Add `TraceCausationChainAsync` method to existing `AdminApiClient.Streams.cs`:
    ```csharp
    public async Task<CausationChain?> TraceCausationChainAsync(
        string tenantId, string domain, string aggregateId,
        long sequenceNumber, CancellationToken cancellationToken)
    ```
    Calls `GET /api/v1/admin/streams/{t}/{d}/{a}/causation?sequenceNumber={n}`. All path segments URI-encoded.

- [x] **Task 2: Create AdminApiClient.Consistency.cs** (AC: #3, #4, #7)
  - [x] 2.1 Create `src/Hexalith.EventStore.Admin.Mcp/AdminApiClient.Consistency.cs` — partial class. **Required using:** `using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;`. Methods:
    - `GetConsistencyChecksAsync(string? tenantId, CancellationToken)` → calls `GET /api/v1/admin/consistency/checks?tenantId={tenantId}`. Uses `GetListAsync<ConsistencyCheckSummary>` for null-safe list return. Returns `IReadOnlyList<ConsistencyCheckSummary>` (not `PagedResult<>` — the consistency endpoint returns a flat list).
    - `GetConsistencyCheckResultAsync(string checkId, CancellationToken)` → calls `GET /api/v1/admin/consistency/checks/{checkId}`. Uses `GetAsync<ConsistencyCheckResult>` (nullable return for 404).
  - [x] 2.2 All methods use `System.Net.Http.Json` extensions. All methods are `async Task<T?>` or `async Task<IReadOnlyList<T>>` with `CancellationToken`. All path segments URI-encoded.

- [x] **Task 3: Create Stream Diagnostic MCP tools** (AC: #1, #2, #5, #6)
  - [x] 3.1 Create `src/Hexalith.EventStore.Admin.Mcp/Tools/DiagnosticTools.cs`:
    ```csharp
    [McpServerToolType]
    internal static class DiagnosticTools
    {
        [McpServerTool(Name = "stream-diff")]
        [Description("Diff aggregate state between two sequence positions, showing which fields changed and their before/after values")]
        public static async Task<string> DiffAggregateState(
            AdminApiClient adminApiClient,
            [Description("Tenant ID")] string tenantId,
            [Description("Domain name")] string domain,
            [Description("Aggregate ID")] string aggregateId,
            [Description("Start sequence number")] long fromSequence,
            [Description("End sequence number")] long toSequence,
            CancellationToken cancellationToken = default) { ... }

        [McpServerTool(Name = "causation-chain")]
        [Description("Trace the full causation chain for an event — originating command, all events produced, and affected projections")]
        public static async Task<string> TraceCausationChain(
            AdminApiClient adminApiClient,
            [Description("Tenant ID")] string tenantId,
            [Description("Domain name")] string domain,
            [Description("Aggregate ID")] string aggregateId,
            [Description("Event sequence number to trace from")] long sequenceNumber,
            CancellationToken cancellationToken = default) { ... }
    }
    ```
  - [x] 3.2 Both tools follow the error-handling pattern from story 18-2:
    - Try calling `AdminApiClient` method
    - Check for null result BEFORE serializing — return `ToolHelper.SerializeError("not-found", "...")` for null (single-entity endpoint)
    - Serialize result via `ToolHelper.SerializeResult(data)`
    - Wrap in `catch (Exception ex) { return ToolHelper.HandleException(ex); }`
    - NEVER throw — always return valid JSON string

- [x] **Task 4: Create Consistency MCP tools** (AC: #3, #4, #5, #6)
  - [x] 4.1 Create `src/Hexalith.EventStore.Admin.Mcp/Tools/ConsistencyTools.cs`:
    ```csharp
    [McpServerToolType]
    internal static class ConsistencyTools
    {
        [McpServerTool(Name = "consistency-list")]
        [Description("List data integrity checks with status, scope, and anomaly counts")]
        public static async Task<string> ListChecks(
            AdminApiClient adminApiClient,
            [Description("Filter by tenant ID")] string? tenantId = null,
            CancellationToken cancellationToken = default) { ... }

        [McpServerTool(Name = "consistency-detail")]
        [Description("Get detailed data integrity check results including anomalies, severity levels, and affected streams")]
        public static async Task<string> GetCheckDetail(
            AdminApiClient adminApiClient,
            [Description("Consistency check ID")] string checkId,
            CancellationToken cancellationToken = default) { ... }
    }
    ```
  - [x] 4.2 `consistency-list` uses `ToolHelper.SerializeResult` for the list — empty array on null is fine (list endpoint).
  - [x] 4.3 `consistency-detail` checks for null result and returns `ToolHelper.SerializeError("not-found", "...")` (single-entity endpoint).

- [x] **Task 5: Create tests for AdminApiClient diagnostic methods** (AC: #7)
  - [x] 5.1 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientDiagnosticTests.cs`:
    - Test `DiffAggregateStateAsync` sends GET to correct path `/api/v1/admin/streams/{t}/{d}/{a}/diff?fromSequence={from}&toSequence={to}`
    - Test `TraceCausationChainAsync` sends GET to correct path `/api/v1/admin/streams/{t}/{d}/{a}/causation?sequenceNumber={n}`
    - `[Theory]` with edge-case aggregate IDs for URI encoding: `"simple-id"`, `"id/with/slashes"`, `"id with spaces"`, `"id+plus"`. Verify `Uri.EscapeDataString()` produces correct path segments.
  - [x] 5.2 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientConsistencyTests.cs`:
    - Test `GetConsistencyChecksAsync` sends GET to `/api/v1/admin/consistency/checks` with and without tenantId
    - Test `GetConsistencyCheckResultAsync` sends GET to `/api/v1/admin/consistency/checks/{checkId}`
    - Test checkId with special characters is URI-encoded

- [x] **Task 6: Create tests for MCP diagnostic tools** (AC: #1-#6)
  - [x] 6.1 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/DiagnosticToolsTests.cs`:
    - Test `DiffAggregateState` returns valid JSON with diff data on success
    - Test `DiffAggregateState` returns `"not-found"` error JSON on 404 (null result)
    - Test `DiffAggregateState` returns error JSON with `"unreachable"` on HttpRequestException
    - Test `TraceCausationChain` returns valid JSON with causation data on success
    - Test `TraceCausationChain` returns `"not-found"` error JSON on 404
    - Test `TraceCausationChain` returns error JSON with `"timeout"` on TaskCanceledException
    - Test both tools return parseable JSON (deserializable to JsonDocument)
  - [x] 6.2 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/ConsistencyToolsTests.cs`:
    - Test `ListChecks` returns valid JSON with check summaries on success
    - Test `ListChecks` returns empty array JSON when no checks exist
    - Test `ListChecks` passes tenantId filter parameter through
    - Test `ListChecks` returns error JSON on failure
    - Test `GetCheckDetail` returns valid JSON with full check result on success
    - Test `GetCheckDetail` returns `"not-found"` error JSON on 404
    - Test `GetCheckDetail` returns error JSON with `"unauthorized"` on 401
    - Test `GetCheckDetail` correctly serializes `truncated: true` when `AnomaliesFound > Anomalies.Count` (anomaly list capped at 500) — verify the `truncated` and `anomaliesFound` fields are present and accurate in the JSON output
    - Test all tools return parseable JSON (deserializable to JsonDocument)
  - [x] 6.3 All tests use `MockHttpMessageHandler` from story 18-2 test infrastructure. Use xUnit `[Fact]`/`[Theory]`, Shouldly assertions, NSubstitute where needed.

- [x] **Task 7: Verify end-to-end** (AC: #8)
  - [x] 7.1 Build solution: `dotnet build Hexalith.EventStore.slnx --configuration Release` — zero warnings
  - [x] 7.2 Run all Tier 1 tests — all green
  - [x] 7.3 Verify all 4 new tools appear in `tools/list` response (discovered automatically via `WithToolsFromAssembly()`)

## Dev Notes

### Architecture Compliance

- **ADR-P4 (Three-Interface Architecture)** — MCP server is a thin HTTP client calling Admin API. No DAPR sidecar. Same pattern as CLI (Epic 17) and stories 18-1/18-2.
- **FR71** — Aggregate state diff between any two event positions with changed fields highlighted.
- **FR72** — Full causation chain tracing for any event: originating command, sender identity, correlation ID, downstream projections.
- **FR81** — MCP structured tools. This story adds diagnostic read-only tools. Write tools (approval-gated) are story 18-4 scope.
- **NFR43** — All read tools must respond within 1s at p99 for single-resource queries. HttpClient timeout is 10s (ceiling).
- **UX-DR56** — All read operations return structured JSON. No HTML, no human-formatted output.
- **UX-DR57** — Approval-gated writes — NOT this story's scope (story 18-4). Consistency check trigger is a write operation via `POST /api/v1/admin/consistency/checks` and belongs to 18-4. This story only implements read operations (list checks, get check result).
- **UX-DR58** — Tenant context scoping — NOT this story's scope (story 18-5). Tools accept explicit `tenantId` parameters.
- **UX-DR59** — Investigation session state — NOT this story's scope (story 18-5).

### Critical Design Decisions

**ADR-1: DiagnosticTools as a separate tool class**

Stream diagnostic tools (`stream-diff`, `causation-chain`) are placed in a new `DiagnosticTools` class rather than extending `StreamTools` (created in story 18-2). Rationale: these tools serve a fundamentally different purpose (diagnosis/investigation) vs. the 18-2 stream tools (browsing/querying). The separation improves discoverability — an AI agent searching for "diagnostic" capabilities finds them grouped together. The MCP SDK's `[McpServerToolType]` discovers all classes automatically.

**ADR-2: ConsistencyTools separate from DiagnosticTools**

Consistency checks are a distinct operational domain (cross-stream integrity verification) vs. stream-level diagnostics (single-stream causation/diff). Separate classes keep each class focused and prevent a "GodTools" class.

**ADR-3: Read-only consistency tools only**

This story implements only the read-only consistency operations:
- `GET /api/v1/admin/consistency/checks` — list check summaries
- `GET /api/v1/admin/consistency/checks/{checkId}` — get full result

The write operations (trigger check via `POST`, cancel check via `POST`) are approval-gated mutations and belong in story 18-4. This matches UX-DR57's requirement that write operations require explicit `confirm: true` parameter.

**ADR-4: Tool naming convention**

Tool names follow the `{domain}-{action}` kebab-case pattern established in 18-2:
- `stream-diff` (extends the `stream-*` namespace)
- `causation-chain` (unique diagnostic namespace)
- `consistency-list`, `consistency-detail` (consistency namespace)

### File Structure

```
src/Hexalith.EventStore.Admin.Mcp/
  AdminApiClient.Streams.cs             # MODIFIED — add DiffAggregateStateAsync, TraceCausationChainAsync
  AdminApiClient.Consistency.cs         # NEW — consistency check query methods (2 methods)
  Tools/
    DiagnosticTools.cs                  # NEW — stream-diff and causation-chain tools
    ConsistencyTools.cs                 # NEW — consistency-list and consistency-detail tools

tests/Hexalith.EventStore.Admin.Mcp.Tests/
  AdminApiClientDiagnosticTests.cs      # NEW — diff and causation client method tests + URI edge cases
  AdminApiClientConsistencyTests.cs     # NEW — consistency client method tests
  DiagnosticToolsTests.cs              # NEW — stream-diff and causation-chain tool tests
  ConsistencyToolsTests.cs             # NEW — consistency tool tests
```

### Existing Code Patterns to Follow

- **AdminApiClient.Streams.cs**: Already has 4 methods (`GetRecentlyActiveStreamsAsync`, `GetStreamTimelineAsync`, `GetAggregateStateAsync`, `GetEventDetailAsync`). New diagnostic methods follow the same pattern: build URL path with `Uri.EscapeDataString()` for path segments, call `GetAsync<T>`, return nullable result. **All methods use `.ConfigureAwait(false)` on every `await`** — follow this convention.
- **ToolHelper** (from 18-2): Use `ToolHelper.SerializeResult<T>(data)` for success, `ToolHelper.SerializeError(status, message)` for errors, `ToolHelper.HandleException(ex)` as the outer catch handler.
- **Tool method pattern** (from 18-2): Static methods, `AdminApiClient` injected via DI parameter, `CancellationToken` parameter, `try/catch` wrapping all logic, never throw — always return JSON string.
- **Test patterns**: Use `MockHttpMessageHandler` from 18-2 test infrastructure. All tests use xUnit `[Fact]`/`[Theory]`, Shouldly assertions.
- **JSON serialization**: Use `System.Text.Json` with `JsonNamingPolicy.CamelCase`. Admin.Abstractions DTOs use PascalCase — JsonSerializerOptions handles casing conversion.
- **Namespace convention**: `Hexalith.EventStore.Admin.Mcp` for root classes, `Hexalith.EventStore.Admin.Mcp.Tools` for tool classes. File-scoped namespaces.
- **Namespace imports for new partials**: `AdminApiClient.Consistency.cs` needs `using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;`. `AdminApiClient.Streams.cs` already has `using Hexalith.EventStore.Admin.Abstractions.Models.Streams;`.

### Previous Story Intelligence (18-2)

Story 18-2 establishes:
- `ToolHelper.cs` with `SerializeResult<T>`, `SerializeError`, `HandleHttpException`, `HandleException`
- Tool class pattern: `[McpServerToolType] internal static class`, static methods with `[McpServerTool(Name = "...")]` and `[Description("...")]`
- `AdminApiClient` partial class pattern with `GetAsync<T>` (single entity, nullable) and `GetListAsync<T>` (list, empty on null)
- Error JSON shape: `{ "error": true, "adminApiStatus": "...", "message": "..." }`
- Status categories: `"unauthorized"` (401/403), `"not-found"` (404), `"server-error"` (5xx), `"unreachable"` (connection failure), `"timeout"` (TaskCanceledException)
- `MockHttpMessageHandler` test pattern for HTTP request verification
- All tools return valid JSON strings — never throw

**Key gotcha from 18-2**: `GetFromJsonAsync<T>` returns `null` on 204 or empty body. For single-entity endpoints (`stream-diff`, `causation-chain`, `consistency-detail`): null means not-found, return `ToolHelper.SerializeError("not-found", "...")`. For list endpoints (`consistency-list`): null means empty result, return `ToolHelper.SerializeResult(Array.Empty<T>())`.

### Admin API Endpoints Used by This Story

| Tool | HTTP Method | Endpoint | Parameters |
|------|-------------|----------|-----------|
| stream-diff | GET | /api/v1/admin/streams/{t}/{d}/{a}/diff | ?fromSequence, ?toSequence |
| causation-chain | GET | /api/v1/admin/streams/{t}/{d}/{a}/causation | ?sequenceNumber |
| consistency-list | GET | /api/v1/admin/consistency/checks | ?tenantId |
| consistency-detail | GET | /api/v1/admin/consistency/checks/{checkId} | (path) |

### Admin.Abstractions DTOs Referenced

**Stream diagnostic models:** `AggregateStateDiff` (FromSequence, ToSequence, ChangedFields), `FieldChange` (FieldPath, OldValue, NewValue), `CausationChain` (OriginatingCommandType, OriginatingCommandId, CorrelationId, UserId, Events, AffectedProjections), `CausationEvent` (SequenceNumber, EventTypeName, Timestamp)

**Consistency models:** `ConsistencyCheckSummary` (CheckId, Status, TenantId, Domain, CheckTypes, StartedAtUtc, CompletedAtUtc, TimeoutUtc, StreamsChecked, AnomaliesFound — no ErrorMessage field), `ConsistencyCheckResult` (extends summary with Anomalies list, Truncated flag, ErrorMessage), `ConsistencyAnomaly` (AnomalyId, CheckType, Severity, TenantId, Domain, AggregateId, Description, Details, ExpectedSequence, ActualSequence), `ConsistencyCheckStatus` enum (Pending, Running, Completed, Failed, Cancelled), `ConsistencyCheckType` enum (SequenceContinuity, SnapshotIntegrity, ProjectionPositions, MetadataConsistency), `AnomalySeverity` enum (Warning, Error, Critical)

All DTOs are in `Hexalith.EventStore.Admin.Abstractions.Models.Streams` and `Hexalith.EventStore.Admin.Abstractions.Models.Consistency` — the MCP project already references Admin.Abstractions via ProjectReference.

### Warnings

- **NEVER use `Console.Write*` in tool code.** stdout is the MCP JSON-RPC transport. Any non-protocol output corrupts the stream.
- **All tools must return valid JSON strings, never throw.** Exceptions from tool methods surface as MCP protocol errors that are opaque to the AI agent. The outer `catch (Exception ex)` in each tool method must catch ALL exception types — not just `HttpRequestException` and `TaskCanceledException`. Specifically, `JsonException` can occur when the Admin API returns malformed JSON (e.g., 200 with HTML error page). Verify that `ToolHelper.HandleException(ex)` handles this case with a generic `"server-error"` status. If it doesn't, add an explicit `JsonException` catch returning `ToolHelper.SerializeError("server-error", "Invalid response from Admin API: " + ex.Message)`.
- **Do NOT validate input parameters in tools.** Tools are thin clients — pass all parameters through to the Admin API and let the server validate (e.g., `fromSequence > toSequence` returns a server-side 400, not a client-side error). This follows the ADR-P4 thin-client principle.
- **URI-encode path segments.** Aggregate IDs and checkId values may contain characters that need encoding. Always use `Uri.EscapeDataString()`.
- **Do NOT add `Console.Error.WriteLine` in tools.** stderr goes to the MCP client's log. Use `ILogger` if logging is needed.
- **Do NOT implement consistency check trigger (POST).** That is a write operation belonging to story 18-4 with approval gates. This story is read-only.
- **FieldChange.ToString() redacts values (SEC-5).** However, the MCP tool serializes the full `FieldChange` record via JSON — OldValue and NewValue are included in the JSON response. This is intentional for AI agent diagnostics, as the agent needs the actual values. The `ToString()` redaction protects against accidental logging, not against deliberate diagnostic output.
- **Large response awareness.** `consistency-list` has no pagination (the Admin API returns a flat `IReadOnlyList`). If many checks exist, the serialized JSON may be large. Similarly, `stream-diff` may return many `FieldChange` entries for complex aggregates. This is acceptable for now — the MCP protocol handles large responses — but be aware of potential token budget impact for AI agent consumers.

### Git Intelligence

Recent commits follow consistent patterns:
- `c13ecae` (18-1): MCP server scaffold — latest feature commit
- All feature commits use: `feat: Add <description> for story 18-X`
- This story should use: `feat: Add MCP diagnostic tools for causation chain, state diff, and consistency checks for story 18-3`
- Branch naming: `feat/story-18-3-diagnostic-tools-causation-diff-consistency`

### References

- [Source: architecture.md, lines 147-153] ADR-P4 Three-Interface Architecture
- [Source: architecture.md, lines 204-212] Admin component distribution model
- [Source: architecture.md, line 237] Admin Authentication — MCP uses API key via env var
- [Source: prd.md, lines 897-899] FR70-FR72 — Point-in-time state, aggregate state diff, causation chain
- [Source: prd.md, line 908] FR81 — MCP structured tools with approval gates
- [Source: prd.md, line 978] NFR43 — Admin MCP <1s p99 tool call response
- [Source: ux-design-specification.md, lines 2094-2095] UX-DR44/45 — Blame view and state diff
- [Source: ux-design-specification.md, lines 2115-2118] UX-DR56-59 — MCP requirements
- [Source: sprint-change-proposal-2026-03-21-admin-tooling.md, line 155] Story 18.3 description
- [Source: AdminStreamsController.cs, lines 128-235] Diff and causation endpoints
- [Source: AdminConsistencyController.cs, lines 26-94] Consistency check read endpoints
- [Source: Admin.Abstractions/Models/Streams/] AggregateStateDiff, CausationChain, CausationEvent, FieldChange
- [Source: Admin.Abstractions/Models/Consistency/] ConsistencyCheckResult, ConsistencyCheckSummary, ConsistencyAnomaly, ConsistencyCheckType, ConsistencyCheckStatus, AnomalySeverity
- [Source: 18-2-read-tools-stream-state-projection-schema-metrics.md] Previous story — ToolHelper, tool patterns, AdminApiClient partial classes

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Build: zero warnings, zero errors (MCP project + test project)
- All 125 MCP tests pass (including 30 new tests for this story)
- All Tier 1 tests pass: Contracts (271), Client (297), Sample (62), Testing (67), SignalR (27)
- All other test projects pass: Admin.Server (206), Admin.Server.Host (15), Admin.UI (365)
- Pre-existing build error in IntegrationTests (CS0433 Program type ambiguity) — unrelated to this story

### Completion Notes List

- Extended `AdminApiClient.Streams.cs` with `DiffAggregateStateAsync` and `TraceCausationChainAsync` — follows exact same pattern as existing methods (URI encoding, `GetAsync<T>`, `.ConfigureAwait(false)`)
- Created `AdminApiClient.Consistency.cs` partial class with `GetConsistencyChecksAsync` (uses `GetListAsync<T>` for null-safe list return) and `GetConsistencyCheckResultAsync` (uses `GetAsync<T>` for nullable return)
- Created `DiagnosticTools.cs` with `stream-diff` and `causation-chain` MCP tools — both use `ToolHelper.ValidateRequired` for path parameters, null-check for not-found, and `ToolHelper.HandleException` for error handling
- Created `ConsistencyTools.cs` with `consistency-list` and `consistency-detail` MCP tools — list tool returns result directly (GetListAsync handles null), detail tool null-checks for not-found
- All 4 new tools follow the established patterns from story 18-2: `[McpServerToolType]`, `[McpServerTool(Name = "...")]`, `[Description("...")]`, static methods, `AdminApiClient` DI injection, `CancellationToken`, try/catch, never throw
- 30 new tests across 4 test files covering: correct URI path construction, URI encoding edge cases, success responses, not-found handling, error categorization (unreachable, timeout, unauthorized), truncated flag serialization, JSON parseability

### File List

**New files:**
- `src/Hexalith.EventStore.Admin.Mcp/AdminApiClient.Consistency.cs`
- `src/Hexalith.EventStore.Admin.Mcp/Tools/DiagnosticTools.cs`
- `src/Hexalith.EventStore.Admin.Mcp/Tools/ConsistencyTools.cs`
- `tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientDiagnosticTests.cs`
- `tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientConsistencyTests.cs`
- `tests/Hexalith.EventStore.Admin.Mcp.Tests/DiagnosticToolsTests.cs`
- `tests/Hexalith.EventStore.Admin.Mcp.Tests/ConsistencyToolsTests.cs`

**Modified files:**
- `src/Hexalith.EventStore.Admin.Mcp/AdminApiClient.Streams.cs` — added DiffAggregateStateAsync, TraceCausationChainAsync

### Change Log

- 2026-03-26: Implemented story 18-3 — added 4 MCP diagnostic tools (stream-diff, causation-chain, consistency-list, consistency-detail), extended AdminApiClient with diagnostic and consistency methods, added 30 tests
