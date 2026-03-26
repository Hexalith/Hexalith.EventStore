# Story 18.5: Tenant Context & Investigation Session State

Status: done

Size: Medium-Large — Creates `InvestigationSession` singleton service, adds `AdminApiClient.Tenants.cs` partial (4 query methods), extends `ToolHelper.HandleHttpException` with 503 mapping, creates 2 new MCP tool classes (7 tools total), modifies 5 existing tool classes to inject session context (all methods in each class) for optional parameter defaults, extends test project with ~8-10 new test classes, ~50-60 tests (~8-10 hours estimated). Implements tenant querying and session state management that enables AI agents to scope investigations to a specific tenant and progressively diagnose issues without repeating parameters.

**Dependency:** Story 18-4 (Write Tools with Approval Gates) must be complete and in `done` status. Stories 18-1 through 18-4 establish ToolHelper, tool class patterns, AdminApiClient partials, and MockHttpMessageHandler. Do NOT start implementation until 18-4 is finalized — this story's tools depend on all previously established patterns. **If the 18-4 code review changed ToolHelper signatures, tool naming conventions, or error shapes, adapt accordingly.**

## Definition of Done

- All acceptance criteria verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- All 7 new MCP tools discoverable via `tools/list` and return structured JSON
- `InvestigationSession` singleton persists state across tool calls within a session
- Session context auto-fills optional `tenantId`/`domain` in existing list/overview tools
- AdminApiClient extended via partial class (no monolithic class)
- All existing Tier 1 and Tier 2 tests continue to pass with zero behavioral change

## Story

As an **AI agent (e.g., Claude) connected to the EventStore via MCP**,
I want **tenant context tools to query tenant metadata and an investigation session that remembers my current scope**,
so that **I can scope my investigation to a specific tenant, query tenant health and quotas, and progressively diagnose issues without repeating tenantId and domain on every tool call — preventing cross-tenant data leakage in AI responses and enabling efficient multi-step diagnosis workflows (FR77, FR79, FR81, UX-DR58, UX-DR59)**.

## Acceptance Criteria

### Tenant Query Tools

1. **`tenant-list` tool** — Calls `GET /api/v1/admin/tenants`. Returns structured JSON array of `TenantSummary` objects (`tenantId`, `displayName`, `status`, `eventCount`, `domainCount`). Tool description: `"List all tenants with status, event counts, and domain counts"`.

2. **`tenant-detail` tool** — Calls `GET /api/v1/admin/tenants/{tenantId}` with required `tenantId` parameter. Returns structured JSON `TenantDetail` (`tenantId`, `displayName`, `status`, `eventCount`, `domainCount`, `storageBytes`, `createdAtUtc`, `quotas`, `subscriptionTier`). On 404, returns structured error JSON with `"not-found"` status. Tool description: `"Get detailed tenant information including quotas, storage, and subscription tier"`.

3. **`tenant-quotas` tool** — Calls `GET /api/v1/admin/tenants/{tenantId}/quotas` with required `tenantId` parameter. Returns structured JSON `TenantQuotas` (`tenantId`, `maxEventsPerDay`, `maxStorageBytes`, `currentUsage`). On 404, returns structured error JSON with `"not-found"` status. Tool description: `"Get tenant quota limits and current usage"`.

4. **`tenant-users` tool** — Calls `GET /api/v1/admin/tenants/{tenantId}/users` with required `tenantId` parameter. Returns structured JSON array of `TenantUser` objects (`email`, `role`, `addedAtUtc`). Tool description: `"List users assigned to a tenant with their roles"`.

### Session Management Tools

5. **`session-set-context` tool** — Sets the active tenant and/or domain in the `InvestigationSession` singleton. Accepts optional `tenantId`, optional `domain`, optional `clearTenantId` (bool, default `false`), and optional `clearDomain` (bool, default `false`) parameters. At least one non-null parameter or one `true` clear flag must be provided. Setting a value and its clear flag simultaneously is an error. Returns a JSON confirmation with the current session state (`tenantId`, `domain`, `startedAtUtc`). Tool description: `"Set or clear tenant and/or domain scope for subsequent queries — reduces parameter repetition and prevents cross-tenant leakage"`.

6. **`session-get-context` tool** — Returns the current `InvestigationSession` state as structured JSON: `tenantId`, `domain`, `startedAtUtc`, `hasContext` boolean. Takes no parameters (except `AdminApiClient` and `InvestigationSession` via DI). Tool description: `"Get the current investigation session context (active tenant and domain scope)"`.

7. **`session-clear-context` tool** — Clears all session state (`tenantId`, `domain`, `startedAtUtc`). Returns confirmation JSON with the cleared state and a `note` field: `"Server-side context cleared. Prior query results in your conversation history may still contain data from the previous tenant scope."`. Tool description: `"Clear the investigation session context to remove tenant and domain scope"`.

### Session Context Integration

8. **`stream-list` uses session context** — When `tenantId` or `domain` parameters are not explicitly provided, `stream-list` falls back to the session's `TenantId` and `Domain`. Explicit parameters always win over session defaults (`??=` semantics).

9. **`projection-list` uses session context** — When `tenantId` is not explicitly provided, `projection-list` falls back to the session's `TenantId`.

10. **`consistency-list` uses session context** — When `tenantId` is not explicitly provided, `consistency-list` falls back to the session's `TenantId`.

11. **`storage-overview` uses session context** — When `tenantId` is not explicitly provided, `storage-overview` falls back to the session's `TenantId`.

12. **`types-list` uses session context** — When `domain` is not explicitly provided, `types-list` falls back to the session's `Domain`.

13. **Detail, diagnostic, and write tools are NOT modified** — Tools with required parameters (`stream-events`, `stream-state`, `stream-event-detail`, `stream-diff`, `causation-chain`, `projection-detail`) are NOT modified — the AI agent reads session context and passes parameters explicitly. This is intentional: detail and diagnostic tools access specific resources where accidental cross-tenant access must be prevented by explicit parameter passing. Write tools (`projection-pause/resume/reset/replay`, `backup-trigger`, `consistency-trigger/cancel`) are also NOT modified — never auto-fill write operation parameters from session context.

14. **`InvestigationSession` injected into ALL methods of modified tool classes** — When adding `InvestigationSession` DI injection to a tool class (e.g., `StreamTools`), add the parameter to **every** method in that class, not just the ones using session fallback. The MCP SDK (`ModelContextProtocol 1.1.0`) may resolve DI at the class level. Methods that don't use the session simply accept and ignore the parameter. This prevents runtime DI resolution failures.

### Cross-Cutting

15. **All tools return structured JSON** — Every tool returns a valid JSON string regardless of success or failure. Error responses use `ToolHelper.SerializeError` shape: `{ "error": true, "adminApiStatus": "<status>", "message": "<detail>" }`.

16. **All tools are decorated with `[Description]`** — Each `[McpServerTool]` method has a `[Description("...")]` attribute.

17. **AdminApiClient extended via partial class** — New tenant API methods are organized into `AdminApiClient.Tenants.cs`.

18. **`InvestigationSession` is a singleton resolvable from DI** — Registered in DI as `builder.Services.AddSingleton<InvestigationSession>()`. Thread-safe. Injected into tool methods as a DI parameter (no `[Description]` attribute — the MCP SDK does not expose DI parameters to the AI agent). Verified by end-to-end test: session tools must function correctly, confirming DI resolution succeeds.

19. **`ToolHelper.HandleHttpException` extended with 503 mapping** — Add `HttpStatusCode.ServiceUnavailable` (503) mapping to `"service-unavailable"` status with message `"Tenant service temporarily unavailable. Retry shortly."`. This is the most likely failure mode for tenant tools since the Hexalith.Tenants peer service may be down.

20. **No regressions** — All existing Tier 1 and Tier 2 tests continue to pass with zero behavioral change.

## Tasks / Subtasks

- [x] **Task 1: Create InvestigationSession service** (AC: #5, #6, #7, #18)
  - [x] 1.1 Create `src/Hexalith.EventStore.Admin.Mcp/InvestigationSession.cs`:
    ```csharp
    namespace Hexalith.EventStore.Admin.Mcp;

    /// <summary>
    /// In-memory session state for MCP investigation context.
    /// Persists across tool calls within a single MCP server process lifetime.
    /// </summary>
    internal sealed class InvestigationSession
    {
        private readonly object _lock = new();

        /// <summary>Gets the active tenant ID scope, or null if unset.</summary>
        public string? TenantId { get; private set; }

        /// <summary>Gets the active domain scope, or null if unset.</summary>
        public string? Domain { get; private set; }

        /// <summary>Gets when the investigation session started, or null if no context set.</summary>
        public DateTimeOffset? StartedAtUtc { get; private set; }

        /// <summary>Gets whether any context is currently set.</summary>
        public bool HasContext => TenantId is not null || Domain is not null;

        /// <summary>
        /// Sets the investigation context. Non-null values replace current values.
        /// Null values leave existing values unchanged (allowing partial updates).
        /// </summary>
        public void SetContext(string? tenantId, string? domain)
        {
            lock (_lock)
            {
                if (tenantId is not null)
                {
                    TenantId = tenantId;
                }

                if (domain is not null)
                {
                    Domain = domain;
                }

                StartedAtUtc ??= DateTimeOffset.UtcNow;
            }
        }

        /// <summary>Clears the tenant ID from session context.</summary>
        public void ClearTenantId()
        {
            lock (_lock)
            {
                TenantId = null;
                if (!HasContext) StartedAtUtc = null;
            }
        }

        /// <summary>Clears the domain from session context.</summary>
        public void ClearDomain()
        {
            lock (_lock)
            {
                Domain = null;
                if (!HasContext) StartedAtUtc = null;
            }
        }

        /// <summary>Clears all session state.</summary>
        public void Clear()
        {
            lock (_lock)
            {
                TenantId = null;
                Domain = null;
                StartedAtUtc = null;
            }
        }
    }
    ```
  - [x] 1.2 Thread safety via `lock` — MCP tool calls may be concurrent. The lock protects against partial reads/writes. This is a precautionary measure (the stdio transport serves a single client) — do not over-engineer thread safety tests. A simple concurrent read/write test suffices.
  - [x] 1.3 **Partial update semantics**: `SetContext(tenantId: "acme", domain: null)` sets tenant but leaves domain unchanged. To clear a single field without clearing everything, use `ClearTenantId()` or `ClearDomain()`. If both fields become null after a single-field clear, `StartedAtUtc` is also reset.

- [x] **Task 2: Register InvestigationSession in Program.cs** (AC: #18)
  - [x] 2.1 Add `builder.Services.AddSingleton<InvestigationSession>();` in `Program.cs` **after** the HttpClient registration and **before** the MCP server registration. This ensures the service is available for DI injection into tool methods.

- [x] **Task 2b: Extend ToolHelper.HandleHttpException with 503 mapping** (AC: #19)
  - [x] 2b.1 In `src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs`, add a case for `HttpStatusCode.ServiceUnavailable` to `HandleHttpException`:
    ```csharp
    HttpStatusCode.ServiceUnavailable
        => SerializeError("service-unavailable", "Service temporarily unavailable. Retry shortly."),
    ```
    **CRITICAL: Place this BEFORE the `not null when (int)ex.StatusCode >= 500` catch-all.** If placed after, the catch-all matches first and 503 never reaches the new case. The switch expression evaluates cases in order — 503 must appear before the generic 5xx pattern.
  - [x] 2b.2 Add `"service-unavailable"` to the status categories list in the "Previous Story Intelligence" section and in ToolHelper XML docs.

- [x] **Task 3: Create AdminApiClient.Tenants.cs** (AC: #1, #2, #3, #4, #17)
  - [x] 3.1 Create `src/Hexalith.EventStore.Admin.Mcp/AdminApiClient.Tenants.cs` — partial class. **Required using:** `using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;`. Methods:
    ```csharp
    /// <summary>Lists all tenants.</summary>
    public async Task<IReadOnlyList<TenantSummary>> ListTenantsAsync(CancellationToken cancellationToken)
    ```
    Calls `GET /api/v1/admin/tenants`. Uses `GetListAsync<TenantSummary>` for null-safe list return.
    ```csharp
    /// <summary>Gets detailed tenant information.</summary>
    public async Task<TenantDetail?> GetTenantDetailAsync(string tenantId, CancellationToken cancellationToken)
    ```
    Calls `GET /api/v1/admin/tenants/{tenantId}`. Uses `GetAsync<TenantDetail>` (nullable return for 404). Path segment URI-encoded via `Uri.EscapeDataString()`.
    ```csharp
    /// <summary>Gets tenant quotas.</summary>
    public async Task<TenantQuotas?> GetTenantQuotasAsync(string tenantId, CancellationToken cancellationToken)
    ```
    Calls `GET /api/v1/admin/tenants/{tenantId}/quotas`. Uses `GetAsync<TenantQuotas>`. Path segment URI-encoded.
    ```csharp
    /// <summary>Gets users assigned to a tenant.</summary>
    public async Task<IReadOnlyList<TenantUser>> GetTenantUsersAsync(string tenantId, CancellationToken cancellationToken)
    ```
    Calls `GET /api/v1/admin/tenants/{tenantId}/users`. Uses `GetListAsync<TenantUser>` for null-safe list return. Path segment URI-encoded.
  - [x] 3.2 All methods use `.ConfigureAwait(false)` on every `await`. All path segments URI-encoded via `Uri.EscapeDataString()`.

- [x] **Task 4: Create TenantTools.cs** (AC: #1, #2, #3, #4, #15, #16)
  - [x] 4.1 Create `src/Hexalith.EventStore.Admin.Mcp/Tools/TenantTools.cs`:
    ```csharp
    [McpServerToolType]
    internal static class TenantTools
    {
        [McpServerTool(Name = "tenant-list")]
        [Description("List all tenants with status, event counts, and domain counts")]
        public static async Task<string> ListTenants(
            AdminApiClient adminApiClient,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await adminApiClient
                    .ListTenantsAsync(cancellationToken)
                    .ConfigureAwait(false);
                return ToolHelper.SerializeResult(result);
            }
            catch (Exception ex)
            {
                return ToolHelper.HandleException(ex);
            }
        }

        [McpServerTool(Name = "tenant-detail")]
        [Description("Get detailed tenant information including quotas, storage, and subscription tier")]
        public static async Task<string> GetTenantDetail(
            AdminApiClient adminApiClient,
            [Description("Tenant ID")] string tenantId,
            CancellationToken cancellationToken = default)
        {
            string? validation = ToolHelper.ValidateRequired((tenantId, "tenantId"));
            if (validation is not null) return validation;

            try
            {
                var result = await adminApiClient
                    .GetTenantDetailAsync(tenantId, cancellationToken)
                    .ConfigureAwait(false);
                return result is null
                    ? ToolHelper.SerializeError("not-found", $"Tenant '{tenantId}' not found")
                    : ToolHelper.SerializeResult(result);
            }
            catch (Exception ex)
            {
                return ToolHelper.HandleException(ex);
            }
        }

        [McpServerTool(Name = "tenant-quotas")]
        [Description("Get tenant quota limits and current usage")]
        public static async Task<string> GetTenantQuotas(
            AdminApiClient adminApiClient,
            [Description("Tenant ID")] string tenantId,
            CancellationToken cancellationToken = default)
        {
            string? validation = ToolHelper.ValidateRequired((tenantId, "tenantId"));
            if (validation is not null) return validation;

            try
            {
                var result = await adminApiClient
                    .GetTenantQuotasAsync(tenantId, cancellationToken)
                    .ConfigureAwait(false);
                return result is null
                    ? ToolHelper.SerializeError("not-found", $"Tenant '{tenantId}' not found")
                    : ToolHelper.SerializeResult(result);
            }
            catch (Exception ex)
            {
                return ToolHelper.HandleException(ex);
            }
        }

        [McpServerTool(Name = "tenant-users")]
        [Description("List users assigned to a tenant with their roles")]
        public static async Task<string> GetTenantUsers(
            AdminApiClient adminApiClient,
            [Description("Tenant ID")] string tenantId,
            CancellationToken cancellationToken = default)
        {
            string? validation = ToolHelper.ValidateRequired((tenantId, "tenantId"));
            if (validation is not null) return validation;

            try
            {
                var result = await adminApiClient
                    .GetTenantUsersAsync(tenantId, cancellationToken)
                    .ConfigureAwait(false);
                return ToolHelper.SerializeResult(result);
            }
            catch (Exception ex)
            {
                return ToolHelper.HandleException(ex);
            }
        }
    }
    ```
  - [x] 4.2 `tenant-list` returns the list directly via `SerializeResult` — `GetListAsync` handles null by returning empty list.
  - [x] 4.3 `tenant-detail` and `tenant-quotas` check for null result and return `ToolHelper.SerializeError("not-found", ...)` (single-entity endpoints).
  - [x] 4.4 `tenant-users` returns the list directly — empty list on no users is valid, not a 404.

- [x] **Task 5: Create SessionTools.cs** (AC: #5, #6, #7, #15, #16)
  - [x] 5.1 Create `src/Hexalith.EventStore.Admin.Mcp/Tools/SessionTools.cs`:
    ```csharp
    [McpServerToolType]
    internal static class SessionTools
    {
        [McpServerTool(Name = "session-set-context")]
        [Description("Set or clear tenant and/or domain scope for subsequent queries — reduces parameter repetition and prevents cross-tenant leakage")]
        public static Task<string> SetContext(
            InvestigationSession session,
            [Description("Tenant ID to scope queries to")] string? tenantId = null,
            [Description("Domain name to scope queries to")] string? domain = null,
            [Description("Set to true to clear the tenant scope")] bool clearTenantId = false,
            [Description("Set to true to clear the domain scope")] bool clearDomain = false)
        {
            // Validate: can't set and clear the same field
            if (tenantId is not null && clearTenantId)
            {
                return Task.FromResult(
                    ToolHelper.SerializeError("invalid-input", "Cannot set 'tenantId' and 'clearTenantId' simultaneously."));
            }

            if (domain is not null && clearDomain)
            {
                return Task.FromResult(
                    ToolHelper.SerializeError("invalid-input", "Cannot set 'domain' and 'clearDomain' simultaneously."));
            }

            // Validate: at least one action
            if (tenantId is null && domain is null && !clearTenantId && !clearDomain)
            {
                return Task.FromResult(
                    ToolHelper.SerializeError("invalid-input", "At least one of 'tenantId', 'domain', 'clearTenantId', or 'clearDomain' must be provided."));
            }

            // Capture previous state for change tracking
            string? previousTenantId = session.TenantId;
            string? previousDomain = session.Domain;

            // Apply clear operations first
            if (clearTenantId) session.ClearTenantId();
            if (clearDomain) session.ClearDomain();

            // Apply set operations
            if (tenantId is not null || domain is not null)
            {
                session.SetContext(tenantId, domain);
            }

            return Task.FromResult(ToolHelper.SerializeResult(new
            {
                contextSet = true,
                tenantId = session.TenantId,
                domain = session.Domain,
                previousTenantId,
                previousDomain,
                startedAtUtc = session.StartedAtUtc,
            }));
        }

        [McpServerTool(Name = "session-get-context")]
        [Description("Get the current investigation session context (active tenant and domain scope)")]
        public static Task<string> GetContext(
            InvestigationSession session)
        {
            return Task.FromResult(ToolHelper.SerializeResult(new
            {
                tenantId = session.TenantId,
                domain = session.Domain,
                startedAtUtc = session.StartedAtUtc,
                hasContext = session.HasContext,
            }));
        }

        [McpServerTool(Name = "session-clear-context")]
        [Description("Clear the investigation session context to remove tenant and domain scope")]
        public static Task<string> ClearContext(
            InvestigationSession session)
        {
            session.Clear();

            return Task.FromResult(ToolHelper.SerializeResult(new
            {
                contextCleared = true,
                tenantId = (string?)null,
                domain = (string?)null,
                startedAtUtc = (DateTimeOffset?)null,
                hasContext = false,
                note = "Server-side context cleared. Prior query results in your conversation history may still contain data from the previous tenant scope.",
            }));
        }
    }
    ```
  - [x] 5.2 Session tools are **synchronous** (no HTTP calls) — they return `Task.FromResult<string>` for consistency with the `Task<string>` return type required by MCP tool methods.
  - [x] 5.3 `session-set-context` validates that at least one parameter is provided — otherwise the call is a no-op and should inform the agent.
  - [x] 5.4 `InvestigationSession` is injected via DI (no `[Description]` attribute). The MCP SDK recognizes it as a service dependency, not a tool parameter.

- [x] **Task 6: Modify existing list/overview tools for session context fallback** (AC: #8-#14)
  - [x] 6.1 **`StreamTools` (all 4 methods)** — Add `InvestigationSession session` parameter (DI-injected, no `[Description]`) to **every** method in the class: `ListStreams`, `GetStreamEvents`, `GetStreamState`, `GetEventDetail`. Only `ListStreams` uses the session fallback:
    ```csharp
    tenantId ??= session.TenantId;
    domain ??= session.Domain;
    ```
    The other 3 methods accept `session` but do not use it — this prevents MCP SDK DI resolution issues if the SDK resolves services per-class rather than per-method. Update `[Description]` on `ListStreams` `tenantId` parameter to: `"Filter by tenant ID (uses session context if omitted)"`. Same for `domain`.
  - [x] 6.2 **`ProjectionTools` (all 2 methods)** — Add `InvestigationSession session` to both `ListProjections` and `GetProjectionDetail`. Only `ListProjections` uses `tenantId ??= session.TenantId;`. Update description.
  - [x] 6.3 **`ConsistencyTools` (all 2 methods)** — Add `InvestigationSession session` to both `ListChecks` and `GetCheckDetail`. Only `ListChecks` uses `tenantId ??= session.TenantId;`. Update description.
  - [x] 6.4 **`StorageTools` (all methods)** — Add `InvestigationSession session` to every method. Only `GetStorageOverview` uses `tenantId ??= session.TenantId;`. Update description.
  - [x] 6.5 **`TypeCatalogTools` (all methods)** — Add `InvestigationSession session` to every method. Only `ListTypes` uses `domain ??= session.Domain;`. Update description.
  - [x] 6.6 **Do NOT modify** `DiagnosticTools`, `ServerTools`, or any write tool class (`ProjectionWriteTools`, `BackupWriteTools`, `ConsistencyWriteTools`). These classes do not need `InvestigationSession` injection — their tools either have all-required parameters or are session-independent. See AC #13 rationale.

- [x] **Task 7: Create tests for InvestigationSession** (AC: #18)
  - [x] 7.1 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/InvestigationSessionTests.cs`:
    - Test `SetContext` sets TenantId and Domain
    - Test `SetContext` with null tenantId preserves existing TenantId (partial update)
    - Test `SetContext` with null domain preserves existing Domain (partial update)
    - Test `SetContext` initializes `StartedAtUtc` on first call, preserves it on subsequent calls
    - Test `ClearTenantId` clears only TenantId, preserves Domain
    - Test `ClearDomain` clears only Domain, preserves TenantId
    - Test `ClearTenantId` also clears `StartedAtUtc` when Domain is already null (no remaining context)
    - Test `Clear` resets all fields to null including `StartedAtUtc`
    - Test `HasContext` returns `false` initially, `true` after SetContext, `false` after Clear
    - Test thread safety: concurrent `SetContext` and `Clear` calls do not corrupt state (simple `Parallel.For` — do not over-engineer)

- [x] **Task 8: Create tests for AdminApiClient tenant methods** (AC: #17)
  - [x] 8.1 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientTenantTests.cs`:
    - Test `ListTenantsAsync` sends GET to `/api/v1/admin/tenants`
    - Test `GetTenantDetailAsync` sends GET to `/api/v1/admin/tenants/{tenantId}`
    - Test `GetTenantQuotasAsync` sends GET to `/api/v1/admin/tenants/{tenantId}/quotas`
    - Test `GetTenantUsersAsync` sends GET to `/api/v1/admin/tenants/{tenantId}/users`
    - `[Theory]` with edge-case tenant IDs for URI encoding: `"simple-tenant"`, `"tenant/with/slashes"`, `"tenant with spaces"`, `"tenant+plus"`. Verify `Uri.EscapeDataString()` produces correct path segments.

- [x] **Task 9: Create tests for TenantTools** (AC: #1-#4, #15, #16)
  - [x] 9.1 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/TenantToolsTests.cs`:
    - Test `tenant-list` returns valid JSON with tenant summaries on success
    - Test `tenant-list` returns empty array JSON when no tenants exist
    - Test `tenant-list` returns error JSON on failure
    - Test `tenant-detail` returns valid JSON with tenant detail on success
    - Test `tenant-detail` returns `"not-found"` error JSON on 404
    - Test `tenant-detail` returns validation error when tenantId is empty
    - Test `tenant-quotas` returns valid JSON with quota data on success
    - Test `tenant-quotas` returns `"not-found"` error JSON on 404
    - Test `tenant-users` returns valid JSON with user list on success
    - Test `tenant-users` returns empty array JSON when no users
    - Test all tools return parseable JSON (deserializable to JsonDocument)
  - [x] 9.2 Test tenant tools return `"service-unavailable"` error JSON on 503 (HttpRequestException with ServiceUnavailable status)
  - [x] 9.3 All tests use `MockHttpMessageHandler` from existing test infrastructure.

- [x] **Task 10: Create tests for SessionTools** (AC: #5, #6, #7, #15)
  - [x] 10.1 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/SessionToolsTests.cs`:
    - Test `session-set-context` returns confirmation with tenantId, domain, previousTenantId, and previousDomain
    - Test `session-set-context` with only tenantId sets tenant, leaves domain null
    - Test `session-set-context` with only domain sets domain, leaves tenant null
    - Test `session-set-context` with all null/false returns error JSON with `"invalid-input"` status
    - Test `session-set-context` partial update preserves existing values
    - Test `session-set-context` with `clearTenantId: true` clears tenant, preserves domain
    - Test `session-set-context` with `clearDomain: true` clears domain, preserves tenant
    - Test `session-set-context` with tenantId and clearTenantId simultaneously returns `"invalid-input"` error
    - Test `session-set-context` with domain and clearDomain simultaneously returns `"invalid-input"` error
    - Test `session-get-context` returns `hasContext: false` initially
    - Test `session-get-context` returns `hasContext: true` after setting context
    - Test `session-get-context` returns correct tenantId and domain
    - Test `session-set-context` switching tenant includes correct `previousTenantId` in response
    - Test `session-clear-context` returns confirmation with all nulls and `note` field about conversation memory
    - Test `session-clear-context` actually clears state (subsequent get returns hasContext: false)
    - Test all returns are parseable JSON (deserializable to JsonDocument)
  - [x] 10.2 All session tests create `InvestigationSession` directly (no mocking — it's a simple in-memory service).

- [x] **Task 11: Create tests for session context integration in existing tools** (AC: #8-#14)
  - [x] 11.1 Create `tests/Hexalith.EventStore.Admin.Mcp.Tests/SessionContextIntegrationTests.cs`:
    - Test `stream-list` uses session tenantId when tenantId parameter is null
    - Test `stream-list` uses explicit tenantId when both session and parameter are provided (explicit wins)
    - Test `stream-list` uses session domain when domain parameter is null
    - Test `projection-list` uses session tenantId when tenantId parameter is null
    - Test `consistency-list` uses session tenantId when tenantId parameter is null
    - Test `storage-overview` uses session tenantId when tenantId parameter is null
    - Test `types-list` uses session domain when domain parameter is null
    - Test all tools work correctly when session has no context (null fallback is harmless)
  - [x] 11.2 Use `MockHttpMessageHandler.CreateCapturingClient` to verify the HTTP request URL includes the correct tenant/domain from session context. **Test setup pattern** (differs from existing tests that only inject `AdminApiClient`):
    ```csharp
    [Fact]
    public async Task StreamList_UsesSessionTenantId_WhenParameterIsNull()
    {
        // Arrange
        string? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            HttpStatusCode.OK, "[]", request => capturedUri = request.RequestUri?.ToString());
        var client = new AdminApiClient(httpClient);
        var session = new InvestigationSession();
        session.SetContext("acme-corp", null);

        // Act
        _ = await StreamTools.ListStreams(client, session, tenantId: null);

        // Assert
        capturedUri.ShouldContain("tenantId=acme-corp");
    }
    ```

- [x] **Task 12: Verify end-to-end** (AC: #20)
  - [x] 12.1 Build solution: `dotnet build Hexalith.EventStore.slnx --configuration Release` — zero warnings
  - [x] 12.2 Run all Tier 1 tests — all green
  - [x] 12.3 Verify all 7 new tools appear in `tools/list` response (discovered automatically via `WithToolsFromAssembly()`)

## Dev Notes

### Architecture Compliance

- **ADR-P4 (Three-Interface Architecture)** — MCP server is a thin HTTP client calling Admin API. No DAPR sidecar. Tenant queries go to Admin API, which delegates to Hexalith.Tenants peer service via its `ITenantQueryService` implementation. The MCP client never bypasses the Admin API layer.
- **FR77** — Tenant lifecycle managed by Hexalith.Tenants peer service. EventStore admin tools consume its API via the Admin API proxy layer. This story implements the MCP client surface for tenant read operations only.
- **FR79** — All admin read operations accessible through MCP (tenant tools complete the coverage).
- **FR81** — MCP structured tools returning machine-readable JSON.
- **UX-DR56** — All read operations return structured JSON.
- **UX-DR58** — Tenant context scoping. `session-set-context` sets the active tenant for subsequent queries, preventing cross-tenant data leakage. List/overview tools auto-scope to the session tenant.
- **UX-DR59** — Investigation session state. The `InvestigationSession` singleton persists context across tool calls within a session. AI agents call `session-set-context` once and then use list/overview tools without repeating the tenant/domain parameters.
- **NFR43** — All MCP tool calls respond within 1s at p99. Session tools are instant (in-memory, no HTTP). Tenant query tools depend on Admin API latency.

### Critical Design Decisions

**ADR-1: InvestigationSession as a DI singleton**

Session state is maintained as an in-memory singleton (`InvestigationSession`) registered in DI. The MCP server is a single-process stdio server — one process per AI agent session. This means:
- Session state naturally aligns with the agent's session lifetime
- No persistence needed — when the process exits, the session is gone
- No multi-user concerns — the stdio transport serves exactly one client
- Thread safety via simple `lock` (tool calls may be concurrent within a session)

Alternative rejected: File-based or database-backed session storage — unnecessary complexity for a single-process stdio server.

**ADR-2: Partial update and selective clear semantics**

`SetContext(tenantId: "acme", domain: null)` sets the tenant but leaves domain unchanged. This allows the AI agent to progressively narrow scope: first set tenant, then add domain. To clear a single field without clearing everything, `session-set-context` accepts `clearTenantId: true` and `clearDomain: true` boolean flags. `InvestigationSession` exposes `ClearTenantId()` and `ClearDomain()` methods that reset individual fields and also clear `StartedAtUtc` if both fields become null. Setting a value and clearing it simultaneously is an error.

**ADR-3: Session context only for optional parameters in list/overview tools**

Session context auto-fills **only** optional `tenantId`/`domain` parameters in tools that list or summarize resources:
- `stream-list`, `projection-list`, `consistency-list`, `storage-overview`, `types-list`

NOT modified:
- **Detail/diagnostic tools** (required parameters) — AI agent passes parameters explicitly. Prevents accidental cross-tenant access on specific-resource queries.
- **Write tools** — Never auto-fill from session. Write operations require fully explicit parameters for safety.

This design balances convenience (list tools auto-scope) with safety (mutations and targeted queries require explicit parameters).

**ADR-4: Explicit parameter always wins over session context**

When both session context and an explicit tool parameter are provided, the explicit parameter wins. This means:
```
session.TenantId = "acme"
stream-list(tenantId: "beta-corp") → queries beta-corp, not acme
```
This prevents session context from silently overriding explicit agent intent.

**ADR-5: Session tools are synchronous (no HTTP calls)**

Session management tools (`set-context`, `get-context`, `clear-context`) operate on in-memory state only. They return `Task.FromResult<string>` for API consistency but make zero HTTP calls. This means:
- Instant response (no network latency)
- No error paths from HTTP failures
- No need for try/catch around HTTP calls

**ADR-6: Tenant write operations are NOT in scope**

Tenant write operations (create, disable, enable, add/remove users, change role) exist in `AdminTenantsController` with Admin policy authorization. These are NOT exposed as MCP tools in this story because:
- They require Admin-level authorization (higher than typical MCP agent access)
- They mutate the Hexalith.Tenants bounded context (not EventStore-owned state)
- They would need approval gates (story 18-4 pattern) and the scope complexity is high
- The primary Journey 9 use case (diagnosis) doesn't require tenant management mutations
- Can be added in a follow-up story if needed

**Future consideration:** If write tool previews later need session-aware context (e.g., "this will affect tenant X based on your current session scope"), write tool classes will need `InvestigationSession` injection. This is a mechanical change (add DI parameter) that can be done in a follow-up story without modifying this story's scope.

### File Structure

```
src/Hexalith.EventStore.Admin.Mcp/
  InvestigationSession.cs               # NEW — session state singleton
  AdminApiClient.Tenants.cs             # NEW — tenant query methods (4 methods)
  Program.cs                            # MODIFIED — register InvestigationSession singleton
  Tools/
    ToolHelper.cs                       # MODIFIED — add 503 → "service-unavailable" mapping
    TenantTools.cs                      # NEW — tenant-list, tenant-detail, tenant-quotas, tenant-users
    SessionTools.cs                     # NEW — session-set-context, session-get-context, session-clear-context
    StreamTools.cs                      # MODIFIED — add InvestigationSession DI param to ALL methods, tenantId/domain fallback in ListStreams
    ProjectionTools.cs                  # MODIFIED — add InvestigationSession DI param to ALL methods, tenantId fallback in ListProjections
    ConsistencyTools.cs                 # MODIFIED — add InvestigationSession DI param to ALL methods, tenantId fallback in ListChecks
    StorageTools.cs                     # MODIFIED — add InvestigationSession DI param to ALL methods, tenantId fallback in GetStorageOverview
    TypeCatalogTools.cs                 # MODIFIED — add InvestigationSession DI param to ALL methods, domain fallback in ListTypes

tests/Hexalith.EventStore.Admin.Mcp.Tests/
  InvestigationSessionTests.cs          # NEW — session state unit tests + thread safety
  AdminApiClientTenantTests.cs          # NEW — tenant client method tests + URI edge cases
  TenantToolsTests.cs                   # NEW — tenant tool tests
  SessionToolsTests.cs                  # NEW — session tool tests
  SessionContextIntegrationTests.cs     # NEW — session fallback tests for existing tools
```

### Existing Code Patterns to Follow

- **AdminApiClient partials** (`.Streams.cs`, `.Projections.cs`, `.Consistency.cs`, `.Types.cs`, `.Storage.cs`): Each partial groups related methods. `AdminApiClient.Tenants.cs` follows the same convention: `internal sealed partial class AdminApiClient` with public async methods. **All methods use `.ConfigureAwait(false)` on every `await`.**
- **ToolHelper** (`ToolHelper.cs`): Use `SerializeResult<T>`, `SerializeError`, `ValidateRequired`, `HandleException`. This story adds a 503 mapping to `HandleHttpException` — no new public helpers, just an extended switch case.
- **Tool method pattern**: Static methods on `[McpServerToolType]` classes. `AdminApiClient` injected via DI parameter. `CancellationToken` as last parameter. Try/catch wrapping all HTTP calls. Never throw — always return JSON string.
- **DI parameter injection**: Parameters without `[Description]` are DI-injected services. The MCP SDK (`ModelContextProtocol 1.1.0`) automatically resolves them from the service provider. `InvestigationSession` follows the same pattern as `AdminApiClient`.
- **Test patterns**: Use `MockHttpMessageHandler` from test infrastructure. `CreateJsonClient` for success responses, `CreateThrowingClient` for exceptions, `CreateCapturingClient` for verifying request details. All tests use xUnit `[Fact]`/`[Theory]`, Shouldly assertions.
- **JSON serialization**: Use `System.Text.Json` with `JsonNamingPolicy.CamelCase`. Tool responses serialized via `ToolHelper.JsonOptions`.
- **Namespace convention**: `Hexalith.EventStore.Admin.Mcp` for root classes (`InvestigationSession`, `AdminApiClient`), `Hexalith.EventStore.Admin.Mcp.Tools` for tool classes. File-scoped namespaces.

### Previous Story Intelligence (18-3 and 18-4)

Stories 18-1 through 18-4 establish:
- `ToolHelper.cs` with `SerializeResult<T>`, `SerializeError`, `SerializePreview`, `ValidateRequired`, `HandleException`, `HandleHttpException`
- Tool class pattern: `[McpServerToolType] internal static class`, static methods with `[McpServerTool(Name = "...")]` and `[Description("...")]`
- `AdminApiClient` partial class pattern with `GetAsync<T>` (single entity, nullable), `GetListAsync<T>` (list, empty on null), `PostAsync` (no body), `PostAsync<T>` (with body)
- Error JSON shape: `{ "error": true, "adminApiStatus": "...", "message": "..." }`
- Status categories: `"unauthorized"` (401/403), `"not-found"` (404), `"invalid-operation"` (422), `"service-unavailable"` (503, added in this story), `"server-error"` (5xx), `"unreachable"` (connection failure), `"timeout"` (TaskCanceledException), `"invalid-input"` (validation)
- `MockHttpMessageHandler` test pattern: `CreateJsonClient`, `CreateThrowingClient`, `CreateCapturingClient`
- All tools return valid JSON strings — never throw

**Key gotcha from 18-2**: `GetFromJsonAsync<T>` returns `null` on 204 or empty body. For single-entity endpoints (`tenant-detail`, `tenant-quotas`): null means not-found, return `ToolHelper.SerializeError("not-found", "...")`. For list endpoints (`tenant-list`, `tenant-users`): null means empty result, return `ToolHelper.SerializeResult(Array.Empty<T>())` via `GetListAsync`.

### Admin API Endpoints Used by This Story

| Tool | HTTP Method | Endpoint | Parameters |
|------|-------------|----------|-----------|
| tenant-list | GET | /api/v1/admin/tenants | (none) |
| tenant-detail | GET | /api/v1/admin/tenants/{tenantId} | (path) |
| tenant-quotas | GET | /api/v1/admin/tenants/{tenantId}/quotas | (path) |
| tenant-users | GET | /api/v1/admin/tenants/{tenantId}/users | (path) |
| session-set-context | (none) | N/A — in-memory only | tenantId?, domain? |
| session-get-context | (none) | N/A — in-memory only | (none) |
| session-clear-context | (none) | N/A — in-memory only | (none) |

### Admin.Abstractions DTOs Referenced

**Tenant models (in `Hexalith.EventStore.Admin.Abstractions.Models.Tenants`):**
- `TenantSummary` (TenantId, DisplayName, Status, EventCount, DomainCount)
- `TenantDetail` (TenantId, DisplayName, Status, EventCount, DomainCount, StorageBytes, CreatedAtUtc, Quotas, SubscriptionTier)
- `TenantQuotas` (TenantId, MaxEventsPerDay, MaxStorageBytes, CurrentUsage)
- `TenantUser` (Email, Role, AddedAtUtc)
- `TenantStatusType` enum (Active, Suspended, Onboarding)

All DTOs are in `Hexalith.EventStore.Admin.Abstractions.Models.Tenants` — the MCP project already references Admin.Abstractions via ProjectReference.

### Warnings

- **NEVER use `Console.Write*` in tool code.** stdout is the MCP JSON-RPC transport. Any non-protocol output corrupts the stream.
- **All tools must return valid JSON strings, never throw.** Exceptions from tool methods surface as MCP protocol errors that are opaque to the AI agent.
- **Do NOT validate input parameters beyond required non-empty checks.** Tools are thin clients — pass parameters through to the Admin API.
- **URI-encode path segments.** Tenant IDs may contain characters that need encoding. Always use `Uri.EscapeDataString()`.
- **Do NOT add `Console.Error.WriteLine` in tools.** stderr goes to the MCP client's log. Use `ILogger` if logging is needed.
- **InvestigationSession must be thread-safe.** MCP tool calls may execute concurrently. The `lock` in `InvestigationSession` protects against data races. Keep critical sections trivially short.
- **Session context is volatile.** When the MCP server process exits, all session state is lost. This is intentional — investigation sessions are transient by design. AI agents should call `session-get-context` as the first tool call after connecting to verify scope. If the MCP process restarted between calls, the agent will see `hasContext: false` and know to re-establish context.
- **Session does not validate tenant status.** Setting context to a tenant ID does not verify the tenant exists or is `Active`. The AI agent should periodically call `tenant-detail` to confirm the scoped tenant is still valid — especially before proposing remediation actions.
- **When tenant tools return `service-unavailable`, proceed with explicit parameters.** If Hexalith.Tenants is down, `tenant-list` and `tenant-detail` will fail. The AI agent should inform the user that tenant scoping is unavailable and pass `tenantId` explicitly on each tool call rather than relying on session context. The `session-set-context` tool itself always works (in-memory, no HTTP) — only tenant query tools are affected.
- **Explicit parameters always override session defaults.** When modifying existing tools, the `??=` operator ensures that a non-null explicit parameter is never overwritten by session state. Verify this in tests.
- **Do NOT expose tenant write operations.** Tenant writes (create, disable, enable, user management) require Admin-level authorization and are NOT in scope for this story. See ADR-6.
- **`tenant-users` cannot detect invalid tenant IDs.** The Admin API's `GET /api/v1/admin/tenants/{tenantId}/users` endpoint returns 200 with an empty list both when the tenant exists but has no users AND when the tenant doesn't exist. The tool cannot distinguish these cases. This is acceptable — the AI agent should use `tenant-detail` first to verify tenant existence if needed.
- **503 Service Unavailable gets distinct status.** After adding the 503 mapping to `HandleHttpException` (AC #19), tenant tools return `"service-unavailable"` instead of generic `"server-error"` when the Hexalith.Tenants peer service is down. This gives the AI agent an actionable signal to retry.

### Git Intelligence

Recent commits follow consistent patterns:
- `751ae68` (18-3): Diagnostic tools — most recent feature commit
- All feature commits use: `feat: Add <description> for story 18-X`
- This story should use: `feat: Add tenant context tools and investigation session state for story 18-5`
- Branch naming: `feat/story-18-5-tenant-context-and-investigation-session-state`

### References

- [Source: architecture.md, lines 147-153] ADR-P4 Three-Interface Architecture — MCP as thin HTTP client
- [Source: architecture.md, lines 212-220] Hexalith.Tenants peer service architecture, MCP consumes Client SDK
- [Source: architecture.md, line 237] Admin Authentication — MCP uses API key via env var
- [Source: prd.md, line 904] FR77 — Tenant lifecycle managed by Hexalith.Tenants
- [Source: prd.md, line 906] FR79 — All admin operations accessible through MCP
- [Source: prd.md, line 908] FR81 — MCP structured tools
- [Source: prd.md, line 978] NFR43 — Admin MCP <1s p99 tool call response
- [Source: prd.md, lines 372-384] Journey 9 — Claude diagnosis workflow with tenant scoping
- [Source: ux-design-specification.md, line 2117] UX-DR58 — Tenant context scoping
- [Source: ux-design-specification.md, line 2118] UX-DR59 — Investigation session state
- [Source: sprint-change-proposal-2026-03-21-tenant-management.md, lines 186-190] Story 18-5 scope change — tenant queries delegated to Hexalith.Tenants API
- [Source: AdminTenantsController.cs, lines 26-416] Tenant read and write endpoints
- [Source: ITenantQueryService.cs] Tenant query interface (5 methods)
- [Source: Admin.Abstractions/Models/Tenants/] TenantSummary, TenantDetail, TenantQuotas, TenantUser, TenantStatusType
- [Source: ToolHelper.cs] Existing error handling and serialization helpers
- [Source: AdminApiClient.cs] Base client with GetAsync<T>, GetListAsync<T>, PostAsync helpers
- [Source: Program.cs] MCP server bootstrap — singleton registration pattern
- [Source: 18-4-write-tools-with-approval-gates.md] Previous story — patterns, ToolHelper.SerializePreview, PostAsync helpers

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Fixed TenantQuotas test JSON: `currentUsage` is `long`, not an object
- Updated ToolHelperTests: 503 now maps to `"service-unavailable"` (was `"server-error"`)
- Fixed nullable warning in SessionToolsTests (CS8604 on ShouldContain)
- Updated all existing tool tests to pass new `InvestigationSession` parameter

### Completion Notes List

- Created `InvestigationSession` singleton with thread-safe `lock` — SetContext, ClearTenantId, ClearDomain, Clear methods
- Registered InvestigationSession in Program.cs as singleton before MCP server registration
- Extended ToolHelper.HandleHttpException with 503 -> "service-unavailable" mapping (placed before generic 5xx catch-all)
- Created AdminApiClient.Tenants.cs partial with 4 methods: ListTenantsAsync, GetTenantDetailAsync, GetTenantQuotasAsync, GetTenantUsersAsync
- Created TenantTools.cs with 4 tools: tenant-list, tenant-detail, tenant-quotas, tenant-users
- Created SessionTools.cs with 3 tools: session-set-context, session-get-context, session-clear-context
- Modified 5 existing tool classes (StreamTools, ProjectionTools, ConsistencyTools, StorageTools, TypeCatalogTools) to inject InvestigationSession into ALL methods
- Session fallback uses `??=` semantics — explicit params always win
- DiagnosticTools, ServerTools, and write tool classes NOT modified per AC #13
- 5 new test files: InvestigationSessionTests, AdminApiClientTenantTests, TenantToolsTests, SessionToolsTests, SessionContextIntegrationTests
- All 307 MCP tests pass, all 724 Tier 1 tests pass, zero warnings in Release build

### Change Log

- 2026-03-26: Story 18-5 implementation complete — tenant context tools, investigation session state, session context integration

### File List

**New files:**
- src/Hexalith.EventStore.Admin.Mcp/InvestigationSession.cs
- src/Hexalith.EventStore.Admin.Mcp/AdminApiClient.Tenants.cs
- src/Hexalith.EventStore.Admin.Mcp/Tools/TenantTools.cs
- src/Hexalith.EventStore.Admin.Mcp/Tools/SessionTools.cs
- tests/Hexalith.EventStore.Admin.Mcp.Tests/InvestigationSessionTests.cs
- tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientTenantTests.cs
- tests/Hexalith.EventStore.Admin.Mcp.Tests/TenantToolsTests.cs
- tests/Hexalith.EventStore.Admin.Mcp.Tests/SessionToolsTests.cs
- tests/Hexalith.EventStore.Admin.Mcp.Tests/SessionContextIntegrationTests.cs

**Modified files:**
- src/Hexalith.EventStore.Admin.Mcp/Program.cs (added InvestigationSession singleton registration)
- src/Hexalith.EventStore.Admin.Mcp/Tools/ToolHelper.cs (added 503 mapping)
- src/Hexalith.EventStore.Admin.Mcp/Tools/StreamTools.cs (added InvestigationSession DI to all 4 methods, session fallback in ListStreams)
- src/Hexalith.EventStore.Admin.Mcp/Tools/ProjectionTools.cs (added InvestigationSession DI to both methods, session fallback in ListProjections)
- src/Hexalith.EventStore.Admin.Mcp/Tools/ConsistencyTools.cs (added InvestigationSession DI to both methods, session fallback in ListChecks)
- src/Hexalith.EventStore.Admin.Mcp/Tools/StorageTools.cs (added InvestigationSession DI, session fallback in GetStorageOverview)
- src/Hexalith.EventStore.Admin.Mcp/Tools/TypeCatalogTools.cs (added InvestigationSession DI, session fallback in ListTypes)
- tests/Hexalith.EventStore.Admin.Mcp.Tests/StreamToolsTests.cs (added InvestigationSession to existing calls)
- tests/Hexalith.EventStore.Admin.Mcp.Tests/ProjectionToolsTests.cs (added InvestigationSession to existing calls)
- tests/Hexalith.EventStore.Admin.Mcp.Tests/ConsistencyToolsTests.cs (added InvestigationSession to existing calls)
- tests/Hexalith.EventStore.Admin.Mcp.Tests/StorageToolsTests.cs (added InvestigationSession to existing calls)
- tests/Hexalith.EventStore.Admin.Mcp.Tests/TypeCatalogToolsTests.cs (added InvestigationSession to existing calls)
- tests/Hexalith.EventStore.Admin.Mcp.Tests/ToolHelperTests.cs (updated 503 expected status to "service-unavailable")
