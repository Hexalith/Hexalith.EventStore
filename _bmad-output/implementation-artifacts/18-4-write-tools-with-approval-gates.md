# Story 18.4: Write Tools with Approval Gates

Status: done

Size: Medium-Large — Adds `PostAsync` helper to AdminApiClient, creates 3 new AdminApiClient partial files (7 POST methods), creates 3 new MCP tool classes (7 tools total), extends test project with ~8 new test classes, ~50-60 tests (~6-8 hours estimated). Implements the approval-gated write tool surface that enables AI agents to control projections, trigger backups, and manage consistency checks — all requiring explicit `confirm: true` before execution.

**Dependency:** Story 18-3 (Diagnostic Tools) must be complete and in `done` status. Stories 18-1 and 18-2 establish the foundational patterns (ToolHelper, tool class structure, AdminApiClient partials, MockHttpMessageHandler). This story extends all of these. **If code review of 18-2 or 18-3 changed ToolHelper signatures, tool naming conventions, or error shapes, adapt accordingly.**

## Definition of Done

- All acceptance criteria verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- All 7 new MCP tools discoverable via `tools/list` and return structured JSON
- All tools enforce approval gate: `confirm: false` (default) returns preview, `confirm: true` executes
- AdminApiClient extended via partial classes (no monolithic class)
- All existing Tier 1 and Tier 2 tests continue to pass with zero behavioral change

## Story

As an **AI agent (e.g., Claude) connected to the EventStore via MCP**,
I want **approval-gated write tools to control projections, trigger backups, and manage consistency checks**,
so that **I can propose and execute remediation actions as part of autonomous incident investigation — but only after the human operator explicitly approves each mutation, preventing unintended side effects (FR81, UX-DR57)**.

## Acceptance Criteria

### Approval Gate Pattern (Cross-Cutting)

1. **All write tools accept a `confirm` boolean parameter (default `false`)** — When `confirm` is `false` or omitted, the tool returns a JSON preview describing what the operation *would* do, including the target endpoint, parameters, and a human-readable description. When `confirm` is `true`, the tool executes the operation and returns the `AdminOperationResult`. This enables the AI agent to present the action to the user for approval before executing.

2. **Preview response shape** — When `confirm` is `false`, the tool returns:
   ```json
   {
     "preview": true,
     "action": "projection-pause",
     "description": "Pause projection 'OrderSummary' for tenant 'acme-corp'",
     "endpoint": "POST /api/v1/admin/projections/acme-corp/OrderSummary/pause",
     "parameters": { "tenantId": "acme-corp", "projectionName": "OrderSummary" },
     "warning": "This will stop the projection from processing new events until resumed."
   }
   ```
   The `warning` field provides risk context specific to each operation type.

### Projection Control Tools

3. **`projection-pause` tool** — Calls `POST /api/v1/admin/projections/{tenantId}/{projectionName}/pause` with required `tenantId` and `projectionName` parameters. Returns `AdminOperationResult` on success. Warning: `"This will stop the projection from processing new events until resumed."`. Tool description: `"Pause a running projection to stop event processing (requires confirm: true)"`.

4. **`projection-resume` tool** — Calls `POST /api/v1/admin/projections/{tenantId}/{projectionName}/resume` with required `tenantId` and `projectionName` parameters. Returns `AdminOperationResult` on success. Warning: `"This will resume event processing for the projection."`. Tool description: `"Resume a paused projection to restart event processing (requires confirm: true)"`. **Design note:** Resume is a low-risk, reversible operation (inverse of pause). The approval gate is applied for consistency across all write tools rather than proportional risk — this avoids the complexity of a mixed-gate surface where some writes need confirmation and others don't.

5. **`projection-reset` tool** — Calls `POST /api/v1/admin/projections/{tenantId}/{projectionName}/reset` with required `tenantId`, `projectionName` and optional `fromPosition` (long?) parameters. Request body is always sent: `{ "fromPosition": <value> }` where value is the position or `null` (meaning reset from beginning). Returns `AdminOperationResult` on success. Warning: `"This will clear projection state and rebuild from the specified position. This is a destructive operation."`. Tool description: `"Reset a projection to rebuild state from a specific event position (requires confirm: true)"`.

6. **`projection-replay` tool** — Calls `POST /api/v1/admin/projections/{tenantId}/{projectionName}/replay` with required `tenantId`, `projectionName`, `fromPosition` (long), `toPosition` (long) parameters. Request body: `{ "fromPosition": <from>, "toPosition": <to> }`. Returns `AdminOperationResult` on success. Warning: `"This will replay events between the specified positions. The projection will reprocess these events."`. Tool description: `"Replay a projection between two event positions (requires confirm: true)"`.

### Backup Tool

7. **`backup-trigger` tool** — Calls `POST /api/v1/admin/backups/{tenantId}?description={desc}&includeSnapshots={bool}` with required `tenantId`, optional `description` (string?) and `includeSnapshots` (bool, default `true`) parameters. Returns `AdminOperationResult` on success. Warning: `"This will initiate a full tenant backup. The operation runs asynchronously."`. Tool description: `"Trigger a full backup for a tenant (requires confirm: true)"`.

### Consistency Check Tools

8. **`consistency-trigger` tool** — Calls `POST /api/v1/admin/consistency/checks` with optional `tenantId`, optional `domain`, required `checkTypes` (comma-separated string of: `SequenceContinuity`, `SnapshotIntegrity`, `ProjectionPositions`, `MetadataConsistency`) parameters. Request body: `{ "tenantId": <t>, "domain": <d>, "checkTypes": [<types>] }`. Returns `AdminOperationResult` on success. Warning: `"This will trigger a data integrity check. Checks run asynchronously and may take time depending on data volume."`. Tool description: `"Trigger a data integrity check across streams and projections (requires confirm: true)"`.

9. **`consistency-cancel` tool** — Calls `POST /api/v1/admin/consistency/checks/{checkId}/cancel` with required `checkId` parameter. Returns `AdminOperationResult` on success. Warning: `"This will cancel the running consistency check. Partial results will be preserved."`. Tool description: `"Cancel a running consistency check (requires confirm: true)"`.

### Cross-Cutting

10. **All tools return structured JSON** — Every tool returns valid JSON regardless of success or failure. Error responses use `ToolHelper.SerializeError` shape: `{ "error": true, "adminApiStatus": "<status>", "message": "<detail>" }`.

11. **All tools are decorated with `[Description]`** — Each `[McpServerTool]` method has a `[Description("...")]` attribute.

12. **AdminApiClient extended via partial classes** — New API methods organized into: `AdminApiClient.ProjectionCommands.cs`, `AdminApiClient.BackupCommands.cs`, `AdminApiClient.ConsistencyCommands.cs`.

13. **No regressions** — All existing Tier 1 and Tier 2 tests continue to pass with zero behavioral change.

## Tasks / Subtasks

- [x] **Task 1: Add `PostAsync` and `PostWithBodyAsync` helpers to AdminApiClient** (AC: #12)
  - [x] 1.1 Add to the existing `AdminApiClient.cs` base class:
    ```csharp
    /// <summary>
    /// Sends a POST request with no body and deserializes the response.
    /// </summary>
    internal async Task<AdminOperationResult?> PostAsync(string path, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient
            .PostAsync(path, content: null, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content
            .ReadFromJsonAsync<AdminOperationResult>(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a POST request with a JSON body and deserializes the response.
    /// </summary>
    internal async Task<AdminOperationResult?> PostAsync<TRequest>(string path, TRequest body, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient
            .PostAsJsonAsync(path, body, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content
            .ReadFromJsonAsync<AdminOperationResult>(cancellationToken)
            .ConfigureAwait(false);
    }
    ```
  - [x] 1.2 Add `using Hexalith.EventStore.Admin.Abstractions.Models.Common;` to `AdminApiClient.cs` imports (for `AdminOperationResult`).
  - [x] 1.3 Both methods use `EnsureSuccessStatusCode()` — this throws `HttpRequestException` with `StatusCode` on 4xx/5xx, which is caught by `ToolHelper.HandleException` in the tool layer. This follows the same delegation pattern as `GetFromJsonAsync` (used in `GetAsync<T>`), which also throws on non-success.

- [x] **Task 2: Create AdminApiClient.ProjectionCommands.cs** (AC: #3-#6, #12)
  - [x] 2.1 Create `src/Hexalith.EventStore.Admin.Mcp/AdminApiClient.ProjectionCommands.cs` — partial class with 4 methods:
    ```csharp
    public async Task<AdminOperationResult?> PauseProjectionAsync(
        string tenantId, string projectionName, CancellationToken cancellationToken)
    ```
    Calls `POST /api/v1/admin/projections/{t}/{p}/pause` with no body. All path segments URI-encoded.
    ```csharp
    public async Task<AdminOperationResult?> ResumeProjectionAsync(
        string tenantId, string projectionName, CancellationToken cancellationToken)
    ```
    Calls `POST /api/v1/admin/projections/{t}/{p}/resume` with no body.
    ```csharp
    public async Task<AdminOperationResult?> ResetProjectionAsync(
        string tenantId, string projectionName, long? fromPosition, CancellationToken cancellationToken)
    ```
    Calls `POST /api/v1/admin/projections/{t}/{p}/reset`. **Always send a JSON body** using `PostAsync<object>(path, new { fromPosition }, cancellationToken)` — when `fromPosition` is null, this serializes as `{"fromPosition":null}`, which the server deserializes as `ProjectionResetRequest(FromPosition: null)` (reset from beginning). Do NOT send a null body — always use the anonymous type for consistency.
    ```csharp
    public async Task<AdminOperationResult?> ReplayProjectionAsync(
        string tenantId, string projectionName, long fromPosition, long toPosition, CancellationToken cancellationToken)
    ```
    Calls `POST /api/v1/admin/projections/{t}/{p}/replay` with body `{ "fromPosition": <from>, "toPosition": <to> }`.
  - [x] 2.2 All methods use `ConfigureAwait(false)` on every `await`. All path segments URI-encoded via `Uri.EscapeDataString()`.

- [x] **Task 3: Create AdminApiClient.BackupCommands.cs** (AC: #7, #12)
  - [x] 3.1 Create `src/Hexalith.EventStore.Admin.Mcp/AdminApiClient.BackupCommands.cs` — partial class with 1 method:
    ```csharp
    public async Task<AdminOperationResult?> TriggerBackupAsync(
        string tenantId, string? description, bool includeSnapshots, CancellationToken cancellationToken)
    ```
    Calls `POST /api/v1/admin/backups/{tenantId}?includeSnapshots={bool}` plus `&description={desc}` when description is non-null. No request body — the Admin API takes these as query parameters. Use `PostAsync(path, cancellationToken)`.
  - [x] 3.2 URI-encode tenantId and description in path/query. Note: the Admin API route regex excludes `export-stream` and `import-stream` as tenantId values — no special handling needed on client side.

- [x] **Task 4: Create AdminApiClient.ConsistencyCommands.cs** (AC: #8, #9, #12)
  - [x] 4.1 Create `src/Hexalith.EventStore.Admin.Mcp/AdminApiClient.ConsistencyCommands.cs` — partial class with 2 methods:
    ```csharp
    public async Task<AdminOperationResult?> TriggerConsistencyCheckAsync(
        string? tenantId, string? domain, IReadOnlyList<string> checkTypes, CancellationToken cancellationToken)
    ```
    Calls `POST /api/v1/admin/consistency/checks` with JSON body: `{ "tenantId": <t>, "domain": <d>, "checkTypes": [<types>] }`. Note: `checkTypes` are string values matching `ConsistencyCheckType` enum names — the server deserializes them. Use an anonymous type for the body.
    ```csharp
    public async Task<AdminOperationResult?> CancelConsistencyCheckAsync(
        string checkId, CancellationToken cancellationToken)
    ```
    Calls `POST /api/v1/admin/consistency/checks/{checkId}/cancel` with no body. URI-encode checkId.

- [x] **Task 5: Create approval gate helper in ToolHelper** (AC: #1, #2)
  - [x] 5.1 Add a `SerializePreview` method to `ToolHelper.cs`:
    ```csharp
    internal static string SerializePreview(
        string action,
        string description,
        string endpoint,
        object parameters,
        string warning)
        => SerializeResult(new { preview = true, action, description, endpoint, parameters, warning });
    ```
    This reuses `SerializeResult` with the standard JSON options (camelCase, indented, enums as strings).

- [x] **Task 6: Create ProjectionWriteTools.cs** (AC: #1-#6, #10, #11)
  - [x] 6.1 Create `src/Hexalith.EventStore.Admin.Mcp/Tools/ProjectionWriteTools.cs`:
    ```csharp
    [McpServerToolType]
    internal static class ProjectionWriteTools
    {
        [McpServerTool(Name = "projection-pause")]
        [Description("Pause a running projection to stop event processing (requires confirm: true)")]
        public static async Task<string> PauseProjection(
            AdminApiClient adminApiClient,
            [Description("Tenant ID")] string tenantId,
            [Description("Projection name")] string projectionName,
            [Description("Set to true to execute; false returns a preview")] bool confirm = false,
            CancellationToken cancellationToken = default)
        {
            string? validation = ToolHelper.ValidateRequired(
                (tenantId, "tenantId"), (projectionName, "projectionName"));
            if (validation is not null) return validation;

            if (!confirm)
            {
                return ToolHelper.SerializePreview(
                    "projection-pause",
                    $"Pause projection '{projectionName}' for tenant '{tenantId}'",
                    $"POST /api/v1/admin/projections/{tenantId}/{projectionName}/pause",
                    new { tenantId, projectionName },
                    "This will stop the projection from processing new events until resumed.");
            }

            try
            {
                AdminOperationResult? result = await adminApiClient
                    .PauseProjectionAsync(tenantId, projectionName, cancellationToken)
                    .ConfigureAwait(false);
                return result is null
                    ? ToolHelper.SerializeError("server-error", "No result returned from the server.")
                    : ToolHelper.SerializeResult(result);
            }
            catch (Exception ex)
            {
                return ToolHelper.HandleException(ex);
            }
        }

        // ... ResumeProjection, ResetProjection, ReplayProjection follow same pattern
    }
    ```
  - [x] 6.2 `projection-resume`: Same pattern as pause. Warning: `"This will resume event processing for the projection."`.
  - [x] 6.3 `projection-reset`: Additional optional `fromPosition` parameter (`long?`). Include `fromPosition` in preview parameters. Warning: `"This will clear projection state and rebuild from the specified position. This is a destructive operation."`.
  - [x] 6.4 `projection-replay`: Additional required `fromPosition` and `toPosition` parameters (`long`). Include both in preview parameters. Warning: `"This will replay events between the specified positions. The projection will reprocess these events."`.

- [x] **Task 7: Create BackupWriteTools.cs** (AC: #1, #2, #7, #10, #11)
  - [x] 7.1 Create `src/Hexalith.EventStore.Admin.Mcp/Tools/BackupWriteTools.cs`:
    ```csharp
    [McpServerToolType]
    internal static class BackupWriteTools
    {
        [McpServerTool(Name = "backup-trigger")]
        [Description("Trigger a full backup for a tenant (requires confirm: true)")]
        public static async Task<string> TriggerBackup(
            AdminApiClient adminApiClient,
            [Description("Tenant ID")] string tenantId,
            [Description("Optional backup description")] string? description = null,
            [Description("Include snapshots in backup")] bool includeSnapshots = true,
            [Description("Set to true to execute; false returns a preview")] bool confirm = false,
            CancellationToken cancellationToken = default) { ... }
    }
    ```
  - [x] 7.2 Warning: `"This will initiate a full tenant backup. The operation runs asynchronously."`.

- [x] **Task 8: Create ConsistencyWriteTools.cs** (AC: #1, #2, #8, #9, #10, #11)
  - [x] 8.1 Create `src/Hexalith.EventStore.Admin.Mcp/Tools/ConsistencyWriteTools.cs`:
    ```csharp
    [McpServerToolType]
    internal static class ConsistencyWriteTools
    {
        [McpServerTool(Name = "consistency-trigger")]
        [Description("Trigger a data integrity check across streams and projections (requires confirm: true)")]
        public static async Task<string> TriggerCheck(
            AdminApiClient adminApiClient,
            [Description("Comma-separated check types: SequenceContinuity, SnapshotIntegrity, ProjectionPositions, MetadataConsistency")] string checkTypes,
            [Description("Filter by tenant ID")] string? tenantId = null,
            [Description("Filter by domain")] string? domain = null,
            [Description("Set to true to execute; false returns a preview")] bool confirm = false,
            CancellationToken cancellationToken = default) { ... }

        [McpServerTool(Name = "consistency-cancel")]
        [Description("Cancel a running consistency check (requires confirm: true)")]
        public static async Task<string> CancelCheck(
            AdminApiClient adminApiClient,
            [Description("Consistency check ID")] string checkId,
            [Description("Set to true to execute; false returns a preview")] bool confirm = false,
            CancellationToken cancellationToken = default) { ... }
    }
    ```
  - [x] 8.2 `consistency-trigger`: First validate `checkTypes` is non-empty via `ToolHelper.ValidateRequired((checkTypes, "checkTypes"))`. Then parse from comma-separated string to `IReadOnlyList<string>` using `checkTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)`. After splitting, if the resulting list is empty, return `ToolHelper.SerializeError("invalid-input", "At least one check type is required. Valid types: SequenceContinuity, SnapshotIntegrity, ProjectionPositions, MetadataConsistency")`. This avoids MCP protocol complications with array parameters — a comma-separated string is simpler for AI agents to construct. Warning: `"This will trigger a data integrity check. Checks run asynchronously and may take time depending on data volume."`.
  - [x] 8.3 `consistency-cancel`: Warning: `"This will cancel the running consistency check. Partial results will be preserved."`.

- [x] **Task 9: Create tests for AdminApiClient POST helpers** (AC: #12)
  - [x] 9.1 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientPostTests.cs`:
    - Test `PostAsync` (no body) sends POST with null content
    - Test `PostAsync<TRequest>` sends POST with JSON body, verify Content-Type is `application/json`
    - Test both methods deserialize `AdminOperationResult` from response
    - Test both methods throw `HttpRequestException` on 4xx/5xx (EnsureSuccessStatusCode)
    - Use `CreateCapturingClient` to verify HTTP method is POST and request body content

- [x] **Task 10: Create tests for AdminApiClient command partials** (AC: #3-#9, #12)
  - [x] 10.1 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientProjectionCommandTests.cs`:
    - Test each of 4 methods sends POST to correct URL path
    - Test URI encoding of tenantId and projectionName with special characters
    - Test `ResetProjectionAsync` with null fromPosition sends null/empty body
    - Test `ResetProjectionAsync` with non-null fromPosition includes body
    - Test `ReplayProjectionAsync` sends body with fromPosition and toPosition
  - [x] 10.2 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientBackupCommandTests.cs`:
    - Test `TriggerBackupAsync` sends POST to correct URL with query parameters
    - Test description query parameter is included when non-null, omitted when null
    - Test includeSnapshots query parameter is correctly formatted
    - Test URI encoding of tenantId
  - [x] 10.3 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientConsistencyCommandTests.cs`:
    - Test `TriggerConsistencyCheckAsync` sends POST to `/api/v1/admin/consistency/checks` with JSON body
    - Test `CancelConsistencyCheckAsync` sends POST to `/api/v1/admin/consistency/checks/{checkId}/cancel`
    - Test checkId with special characters is URI-encoded

- [x] **Task 11: Create tests for MCP write tools** (AC: #1-#11)
  - [x] 11.1 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/ProjectionWriteToolsTests.cs`:
    - Test each tool returns preview JSON when `confirm: false` (verify `preview: true`, `action`, `description`, `endpoint`, `parameters`, `warning` fields)
    - Test each tool executes and returns `AdminOperationResult` JSON when `confirm: true`
    - Test each tool returns validation error JSON when required params are empty
    - Test each tool returns error JSON on `HttpRequestException` (e.g., 401, 404, 500)
    - Test each tool returns error JSON on `TaskCanceledException` (timeout)
    - Test all returns are parseable JSON (deserializable to `JsonDocument`)
  - [x] 11.2 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/BackupWriteToolsTests.cs`:
    - Same patterns as projection tests: preview vs execute, validation, error handling
    - Test optional description parameter flows through correctly
    - Test includeSnapshots default (true) and explicit false
  - [x] 11.3 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/ConsistencyWriteToolsTests.cs`:
    - Same patterns: preview vs execute, validation, error handling
    - Test `consistency-trigger` parses comma-separated checkTypes correctly
    - Test `consistency-trigger` with single and multiple check types
    - Test `consistency-cancel` with checkId validation
  - [x] 11.4 All tests use `MockHttpMessageHandler` from existing test infrastructure. Use xUnit `[Fact]`/`[Theory]`, Shouldly assertions.

- [x] **Task 12: Verify end-to-end** (AC: #13)
  - [x] 12.1 Build solution: `dotnet build Hexalith.EventStore.slnx --configuration Release` — zero warnings
  - [x] 12.2 Run all Tier 1 tests — all green
  - [x] 12.3 Verify all 7 new tools appear in `tools/list` response (discovered automatically via `WithToolsFromAssembly()`)

## Dev Notes

### Architecture Compliance

- **ADR-P4 (Three-Interface Architecture)** — MCP server is a thin HTTP client calling Admin API. No DAPR sidecar. Write operations go through Admin API which delegates to CommandApi via `DaprClient.InvokeMethodAsync`. The MCP client never bypasses the command pipeline's security and validation layers.
- **FR81** — MCP structured tools with approval-gated write operations. This story implements the approval gate pattern via `confirm: true` parameter.
- **UX-DR57** — Write operations require explicit `confirm: true` parameter. AI agent must present action to user for approval before executing. The preview response provides all context needed for the user to make an informed decision.
- **NFR43** — All tool calls must respond within 1s at p99 for single-resource operations. Preview responses are instant (no HTTP call). Write operations depend on Admin API latency.
- **UX-DR56** — All operations return structured JSON.

### Critical Design Decisions

**ADR-1: Approval gate via `confirm` boolean parameter**

Every write tool accepts `confirm: bool = false`. When false, the tool returns a structured preview of what *would* happen without making any HTTP call. When true, the tool executes the operation. This implements UX-DR57's requirement: "AI agent must present action to user for approval before executing." The AI agent's workflow is:
1. Call tool with `confirm: false` (or omit) — gets preview
2. Present preview + warning to the human operator
3. On approval, call tool again with `confirm: true` — executes

**Trust assumption:** The approval gate trusts the MCP client (AI agent) to present the preview to the human before calling with `confirm: true`. The server cannot enforce that the preview was shown — this is an inherent limitation of the client-side gate pattern. A server-side two-phase approval (e.g., confirmation tokens) would be more robust but is out of scope.

**Staleness caveat:** The preview is a static template generated client-side — it does not reflect current server state. Between the preview call and the execute call, the target resource may have changed (e.g., another operator paused the projection). The server validates current state on execute and returns appropriate errors (e.g., `422 InvalidOperation`). Future enhancement: preview could call a read endpoint first to include live context (e.g., current projection position, event count to replay).

**ADR-2: Separate write tool classes from read tool classes**

Write tools are in separate classes (`ProjectionWriteTools`, `BackupWriteTools`, `ConsistencyWriteTools`) rather than merged into existing read tool classes (`ProjectionTools`, `ConsistencyTools`). Rationale: (a) read and write operations have fundamentally different risk profiles — separating them makes the distinction visible, (b) write tools share the approval gate pattern which is structurally different from read tools, (c) the MCP SDK discovers all `[McpServerToolType]` classes automatically so separation has no registration cost.

**ADR-3: Separate AdminApiClient partials for commands vs queries**

Write methods are in `AdminApiClient.ProjectionCommands.cs` (not added to existing `AdminApiClient.Projections.cs`). This keeps query methods and command methods separated, matching the CQRS pattern used throughout the codebase. Each partial file stays focused.

**ADR-4: `checkTypes` as comma-separated string, not array**

The `consistency-trigger` tool accepts check types as a comma-separated string (`"SequenceContinuity,ProjectionPositions"`) rather than an array parameter. MCP tool parameters are simpler as scalar types — arrays require complex JSON in parameter values. The tool splits and trims the string before passing to the API client.

**ADR-5: Request DTOs are in Admin.Server.Models, not Abstractions**

The request DTOs (`ProjectionResetRequest`, `ProjectionReplayRequest`, `ConsistencyCheckRequest`, `DeadLetterActionRequest`) are defined in `Hexalith.EventStore.Admin.Server.Models` — NOT in Admin.Abstractions. The MCP client does NOT reference Admin.Server. Instead, write tool methods construct anonymous types for POST bodies. This is intentional: the MCP client is a thin HTTP client that should not depend on server-side models.

**ADR-6: Scope — projection controls, backup trigger, consistency check operations**

This story covers the 7 write operations explicitly scoped by the epic description and deferred from story 18-3:
- 4 projection controls (FR73): pause, resume, reset, replay
- 1 backup trigger (FR76): trigger backup
- 2 consistency check operations: trigger, cancel (deferred from 18-3 per UX-DR57)

NOT in scope: backup restore, backup validate, stream export/import (heavy DBA operations requiring Admin role, not typical for AI agent diagnosis), dead-letter retry/skip/archive (FR78 — can be added in a follow-up story if Journey 9 scenarios reveal that dead-lettered events are a common root cause requiring AI agent remediation). These can be added later as the MCP tool surface matures.

### File Structure

```
src/Hexalith.EventStore.Admin.Mcp/
  AdminApiClient.cs                        # MODIFIED — add PostAsync and PostAsync<T> helpers
  AdminApiClient.ProjectionCommands.cs     # NEW — 4 projection write methods
  AdminApiClient.BackupCommands.cs         # NEW — 1 backup trigger method
  AdminApiClient.ConsistencyCommands.cs    # NEW — 2 consistency write methods
  Tools/
    ToolHelper.cs                          # MODIFIED — add SerializePreview method
    ProjectionWriteTools.cs                # NEW — projection-pause, -resume, -reset, -replay
    BackupWriteTools.cs                    # NEW — backup-trigger
    ConsistencyWriteTools.cs               # NEW — consistency-trigger, consistency-cancel

tests/Hexalith.EventStore.Admin.Mcp.Tests/
  AdminApiClientPostTests.cs               # NEW — PostAsync helper tests
  AdminApiClientProjectionCommandTests.cs  # NEW — projection command method tests
  AdminApiClientBackupCommandTests.cs      # NEW — backup command method tests
  AdminApiClientConsistencyCommandTests.cs # NEW — consistency command method tests
  ProjectionWriteToolsTests.cs             # NEW — projection write tool tests (preview + execute)
  BackupWriteToolsTests.cs                 # NEW — backup write tool tests
  ConsistencyWriteToolsTests.cs            # NEW — consistency write tool tests
  ToolHelperTests.cs                       # MODIFIED — add SerializePreview test
```

### Existing Code Patterns to Follow

- **AdminApiClient base** (`AdminApiClient.cs`): Currently has `GetAsync<T>` and `GetListAsync<T>` helpers. New `PostAsync` and `PostAsync<T>` helpers follow the same pattern — internal methods on the base class that handle HTTP communication and deserialization.
- **AdminApiClient partials** (`.Projections.cs`, `.Streams.cs`, etc.): Each partial groups related methods. Write partials follow the same convention: `internal sealed partial class AdminApiClient` with public async methods. **All methods use `.ConfigureAwait(false)` on every `await`.**
- **ToolHelper** (`ToolHelper.cs`): Use `SerializeResult<T>`, `SerializeError`, `ValidateRequired`, `HandleException`. New `SerializePreview` reuses `SerializeResult` for consistency.
- **Tool method pattern**: Static methods on `[McpServerToolType]` classes. `AdminApiClient` injected via DI parameter. `CancellationToken` as last parameter. Try/catch wrapping all HTTP calls. Never throw — always return JSON string.
- **Test patterns**: Use `MockHttpMessageHandler` from test infrastructure. `CreateJsonClient` for success responses, `CreateThrowingClient` for exceptions, `CreateCapturingClient` for verifying request details (method, URI, body). All tests use xUnit `[Fact]`/`[Theory]`, Shouldly assertions.
- **JSON serialization**: Use `System.Text.Json` with `JsonNamingPolicy.CamelCase`. Tool responses are serialized via `ToolHelper.JsonOptions`.
- **Namespace convention**: `Hexalith.EventStore.Admin.Mcp` for client classes, `Hexalith.EventStore.Admin.Mcp.Tools` for tool classes. File-scoped namespaces.

### Admin API Endpoints Used by This Story

| Tool | HTTP Method | Endpoint | Body | Auth |
|------|-------------|----------|------|------|
| projection-pause | POST | /api/v1/admin/projections/{t}/{p}/pause | none | Operator |
| projection-resume | POST | /api/v1/admin/projections/{t}/{p}/resume | none | Operator |
| projection-reset | POST | /api/v1/admin/projections/{t}/{p}/reset | `{ "fromPosition": <n?> }` | Operator |
| projection-replay | POST | /api/v1/admin/projections/{t}/{p}/replay | `{ "fromPosition": <n>, "toPosition": <n> }` | Operator |
| backup-trigger | POST | /api/v1/admin/backups/{t}?description=&includeSnapshots= | none | Admin |
| consistency-trigger | POST | /api/v1/admin/consistency/checks | `{ "tenantId": <t?>, "domain": <d?>, "checkTypes": [<strings>] }` | Operator |
| consistency-cancel | POST | /api/v1/admin/consistency/checks/{id}/cancel | none | Operator |

### Admin API Response Model

All write operations return `AdminOperationResult`:
```csharp
public record AdminOperationResult(bool Success, string OperationId, string? Message, string? ErrorCode);
```
- Sync operations (pause, resume, cancel): return `200 OK` with result
- Async operations (reset, replay, backup trigger, consistency trigger): return `202 Accepted` with result
- Both return the same DTO shape — the MCP client treats them identically (deserialize as `AdminOperationResult`)

### Previous Story Intelligence (18-2 and 18-3)

Stories 18-2 and 18-3 establish:
- `ToolHelper.cs` with `SerializeResult<T>`, `SerializeError`, `ValidateRequired`, `HandleException`, `HandleHttpException`
- Tool class pattern: `[McpServerToolType] internal static class`, static methods with `[McpServerTool(Name = "...")]` and `[Description("...")]`
- `AdminApiClient` partial class pattern with `GetAsync<T>` (single entity, nullable) and `GetListAsync<T>` (list, empty on null)
- Error JSON shape: `{ "error": true, "adminApiStatus": "...", "message": "..." }`
- Status categories: `"unauthorized"` (401/403), `"not-found"` (404), `"invalid-operation"` (422, new in this story), `"server-error"` (5xx), `"unreachable"` (connection failure), `"timeout"` (TaskCanceledException), `"invalid-input"` (validation)
- `MockHttpMessageHandler` test pattern for HTTP request verification
- All tools return valid JSON strings — never throw

**Key pattern from 18-2/18-3 that differs for write tools**: Read tools check for null result and return `SerializeError("not-found", ...)`. Write tools should check for null `AdminOperationResult` and return `SerializeError("server-error", "No result returned from the server.")` — a null write result is a server error, not a not-found.

### Warnings

- **NEVER use `Console.Write*` in tool code.** stdout is the MCP JSON-RPC transport. Any non-protocol output corrupts the stream.
- **All tools must return valid JSON strings, never throw.** The outer `catch (Exception ex)` in each tool method must catch ALL exception types. `ToolHelper.HandleException(ex)` handles `HttpRequestException`, `JsonException`, `TaskCanceledException`, and falls through to generic error.
- **Do NOT validate operation parameters in tools.** Tools are thin clients — pass all parameters through to the Admin API and let the server validate (e.g., invalid projection name returns a server-side 404, not a client-side error). Only validate that required path-segment parameters are non-empty via `ToolHelper.ValidateRequired`.
- **URI-encode path segments.** Tenant IDs, projection names, and check IDs may contain special characters. Always use `Uri.EscapeDataString()`.
- **Do NOT use `Console.Error.WriteLine` in tools.** stderr goes to the MCP client's log. Use `ILogger` if logging is needed.
- **Preview responses must NOT make any HTTP call.** The preview path should return immediately with the structured description. Only the `confirm: true` path makes HTTP calls.
- **Anonymous types for POST bodies.** Since request DTOs live in `Admin.Server.Models` (not referenced by MCP project), use anonymous types for POST bodies: `new { fromPosition }`, `new { fromPosition, toPosition }`, `new { tenantId, domain, checkTypes }`. These serialize correctly with `System.Text.Json`.
- **Extend `ToolHelper.HandleHttpException` for 422 status.** Write operations commonly return `422 Unprocessable Entity` (e.g., "cannot pause an already-paused projection"). The current `HandleHttpException` maps 422 to the generic `"server-error"` fallback. Add a case for `HttpStatusCode.UnprocessableEntity` (or `(HttpStatusCode)422`) mapping to `"invalid-operation"` status: `SerializeError("invalid-operation", $"Operation rejected: {ex.Message}")`. This gives AI agents a distinct, actionable error category for write-specific failures.
- **`EnsureSuccessStatusCode()` vs `GetFromJsonAsync` behavior.** The existing `GetAsync<T>` uses `GetFromJsonAsync` which throws on non-success. The new `PostAsync` methods use `PostAsync`/`PostAsJsonAsync` + `EnsureSuccessStatusCode()` + `ReadFromJsonAsync` to achieve the same behavior. Both throw `HttpRequestException` with StatusCode on failure.
- **Test POST body verification.** To verify POST request bodies in tests, use `CreateCapturingClient` and read `request.Content` as a string. The capturing callback is `Action<HttpRequestMessage>` (synchronous), so use `request.Content!.ReadAsStringAsync().Result` to read the body — this is safe in test context since the content is already buffered in memory. Parse the resulting JSON string to verify field values. Remember that `PostAsJsonAsync` will set `Content-Type: application/json; charset=utf-8`.

### Git Intelligence

Recent commits follow consistent patterns:
- `7ad6389` (18-2): Read tools — latest feature commit
- `c13ecae` (18-1): MCP server scaffold
- All feature commits use: `feat: Add <description> for story 18-X`
- This story should use: `feat: Add MCP approval-gated write tools for projections, backups, and consistency checks for story 18-4`
- Branch naming: `feat/story-18-4-write-tools-with-approval-gates`

### References

- [Source: architecture.md, lines 147-153] ADR-P4 Three-Interface Architecture — MCP as thin HTTP client
- [Source: architecture.md, line 153] Write operations delegated to CommandApi via DaprClient.InvokeMethodAsync
- [Source: architecture.md, line 237] Admin Authentication — MCP uses API key via env var
- [Source: prd.md, line 908] FR81 — MCP structured tools with approval-gated write operations
- [Source: prd.md, line 900] FR73 — Projection controls: pause, resume, reset, replay
- [Source: prd.md, line 903] FR76 — Storage management including backup operations
- [Source: prd.md, line 978] NFR43 — Admin MCP <1s p99 tool call response
- [Source: prd.md, lines 376-384] Journey 9 — Claude diagnosis workflow with approval gates
- [Source: ux-design-specification.md, line 2117] UX-DR57 — Approval-gated writes require explicit confirm: true
- [Source: ux-design-specification.md, line 2115] UX-DR56 — All read operations return structured JSON
- [Source: sprint-change-proposal-2026-03-21-admin-tooling.md, line 156] Story 18.4 description
- [Source: AdminProjectionsController.cs, lines 96-223] Projection write endpoints (pause, resume, reset, replay)
- [Source: AdminBackupsController.cs, lines 60-88] Backup trigger endpoint
- [Source: AdminConsistencyController.cs, lines 96-168] Consistency check trigger and cancel endpoints
- [Source: Admin.Server/Models/ProjectionResetRequest.cs] ProjectionResetRequest(long? FromPosition)
- [Source: Admin.Server/Models/ProjectionReplayRequest.cs] ProjectionReplayRequest(long FromPosition, long ToPosition)
- [Source: Admin.Server/Models/ConsistencyCheckRequest.cs] ConsistencyCheckRequest(string? TenantId, string? Domain, IReadOnlyList<ConsistencyCheckType> CheckTypes)
- [Source: AdminOperationResult.cs] AdminOperationResult(bool Success, string OperationId, string? Message, string? ErrorCode)
- [Source: ToolHelper.cs] Existing error handling and serialization helpers
- [Source: ProjectionTools.cs] Read-only projection tool pattern reference
- [Source: AdminApiClient.cs] Base client with GetAsync<T> and GetListAsync<T>
- [Source: 18-3-diagnostic-tools-causation-diff-consistency.md] Previous story — patterns and lessons

## Change Log

- 2026-03-26: Implemented all 12 tasks — 7 MCP write tools with approval gates, 3 AdminApiClient command partials, PostAsync helpers, SerializePreview + 422 handling. 234 Admin.Mcp tests green, 958 total Tier 1 tests green, zero regressions.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Build: `dotnet build src/Hexalith.EventStore.Admin.Mcp/ --configuration Release` — 0 warnings, 0 errors
- Tests: `dotnet test tests/Hexalith.EventStore.Admin.Mcp.Tests/` — 234 passed
- Regression: All 6 Tier 1 test projects — 958 total tests passed

### Completion Notes List

- Task 1: Added `PostAsync` (no body) and `PostAsync<TRequest>` (JSON body) internal helpers to `AdminApiClient.cs`. Both use `EnsureSuccessStatusCode()` to throw `HttpRequestException` on non-success, matching the `GetFromJsonAsync` pattern.
- Task 2: Created `AdminApiClient.ProjectionCommands.cs` with 4 methods (pause, resume, reset, replay). Reset and replay send JSON bodies via anonymous types. All path segments URI-encoded.
- Task 3: Created `AdminApiClient.BackupCommands.cs` with `TriggerBackupAsync`. Description and includeSnapshots as query parameters.
- Task 4: Created `AdminApiClient.ConsistencyCommands.cs` with `TriggerConsistencyCheckAsync` (JSON body) and `CancelConsistencyCheckAsync` (no body).
- Task 5: Added `ToolHelper.SerializePreview` for approval gate preview responses. Extended `HandleHttpException` with 422 → "invalid-operation" mapping.
- Task 6: Created `ProjectionWriteTools.cs` with 4 tools (projection-pause, -resume, -reset, -replay). All implement confirm gate pattern.
- Task 7: Created `BackupWriteTools.cs` with `backup-trigger` tool.
- Task 8: Created `ConsistencyWriteTools.cs` with `consistency-trigger` (comma-separated checkTypes parsing) and `consistency-cancel` tools.
- Tasks 9-11: Created 7 test files with comprehensive coverage: PostAsync helpers, 3 AdminApiClient command partials, 3 MCP tool classes. Tests cover preview/execute paths, validation, error handling, URI encoding, JSON parseability.
- Task 12: Full solution builds with zero warnings. 234 Admin.Mcp tests pass. All Tier 1 tests (958 total) pass with zero regressions. All 7 new tools discovered automatically via `WithToolsFromAssembly()`.

### File List

**New files:**
- src/Hexalith.EventStore.Admin.Mcp/AdminApiClient.ProjectionCommands.cs
- src/Hexalith.EventStore.Admin.Mcp/AdminApiClient.BackupCommands.cs
- src/Hexalith.EventStore.Admin.Mcp/AdminApiClient.ConsistencyCommands.cs
- src/Hexalith.EventStore.Admin.Mcp/Tools/ProjectionWriteTools.cs
- src/Hexalith.EventStore.Admin.Mcp/Tools/BackupWriteTools.cs
- src/Hexalith.EventStore.Admin.Mcp/Tools/ConsistencyWriteTools.cs
- tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientPostTests.cs
- tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientProjectionCommandTests.cs
- tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientBackupCommandTests.cs
- tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientConsistencyCommandTests.cs
- tests/Hexalith.EventStore.Admin.Mcp.Tests/ProjectionWriteToolsTests.cs
- tests/Hexalith.EventStore.Admin.Mcp.Tests/BackupWriteToolsTests.cs
- tests/Hexalith.EventStore.Admin.Mcp.Tests/ConsistencyWriteToolsTests.cs

**Modified files:**
- src/Hexalith.EventStore.Admin.Mcp/AdminApiClient.cs (added PostAsync helpers, Common using)
- src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs (added SerializePreview, 422 handling)
- tests/Hexalith.EventStore.Admin.Mcp.Tests/ToolHelperTests.cs (added SerializePreview + 422 tests)
