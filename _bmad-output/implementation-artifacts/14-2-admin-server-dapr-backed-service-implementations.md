# Story 14.2: Admin.Server — DAPR-Backed Service Implementations

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer building admin tooling (Web UI, CLI, or MCP),
I want DAPR-backed implementations of all admin service interfaces in a new `Hexalith.EventStore.Admin.Server` project,
So that all three admin interfaces share a single, tested implementation that reads event store state via DAPR and delegates writes through the command pipeline.

## Acceptance Criteria

1. **Given** the solution, **When** built, **Then** a new project `src/Hexalith.EventStore.Admin.Server/` exists, compiles with zero errors/warnings, and is included in `Hexalith.EventStore.slnx`.
2. **Given** Admin.Server, **When** inspected, **Then** it provides concrete implementations for all 9 service interfaces defined in Admin.Abstractions: `IStreamQueryService`, `IProjectionQueryService`, `IProjectionCommandService`, `ITypeCatalogService`, `IHealthQueryService`, `IStorageQueryService`, `IStorageCommandService`, `IDeadLetterService`, `ITenantQueryService`.
3. **Given** each service implementation that reads event/aggregate data (streams, state snapshots, causation chains), **When** invoked, **Then** it delegates to CommandApi via `DaprClient.InvokeMethodAsync()` — because event data lives in DAPR actor state which is NOT accessible via plain `DaprClient.GetStateAsync()` (actor state uses a different key namespace). Services reading non-actor data (admin indexes, command status) may use `DaprClient.GetStateAsync()` with `AdminStateStoreKeys` (NFR44).
4. **Given** each write service implementation (`IProjectionCommandService`, `IStorageCommandService`, `IDeadLetterService`), **When** invoked, **Then** it delegates to CommandApi via `DaprClient.InvokeMethodAsync()` — never writing to the state store directly (ADR-P4).
4a. **Given** any service delegating to CommandApi or Tenants service, **When** invoked, **Then** it forwards the caller's JWT token via DAPR metadata headers to preserve authorization context.
5. **Given** `ITenantQueryService` implementation, **When** invoked, **Then** it delegates to the Hexalith.Tenants peer service via DAPR service invocation — EventStore does NOT own tenant state (FR77).
6. **Given** any service method receiving an unknown key, **When** the state store returns null/default, **Then** the service returns an appropriate empty result (empty list, null DTO, or `AdminOperationResult` with `Success=false`) — never throws.
7. **Given** Admin.Server, **When** referenced, **Then** it depends on: `Admin.Abstractions`, `Contracts`, `Dapr.Client`, and `Microsoft.Extensions.DependencyInjection.Abstractions` — no ASP.NET Core controller dependencies (those are Story 14-3), no ProjectReference to Server.
8. **Given** `ServiceCollectionExtensions.AddAdminServer()`, **When** called, **Then** all 9 service implementations are registered in DI as their interface types (scoped lifetime), and `AdminServerOptions` is bound from configuration section `"AdminServer"`.
9. **Given** a new Tier 1 test project `tests/Hexalith.EventStore.Admin.Server.Tests/`, **When** tests run, **Then** all service implementations are tested with mocked `DaprClient` (NSubstitute) covering: correct key derivation, DTO mapping from state store data, null/missing state handling, and write delegation to CommandApi.

## Tasks / Subtasks

- [ ] Task 0: Prerequisites (AC: all)
  - [ ] 0.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` — confirm baseline compiles (0 errors, 0 warnings)
  - [ ] 0.2 Read Story 14-1 output: verify `src/Hexalith.EventStore.Admin.Abstractions/` exists with all 9 service interfaces and DTOs. If 14-1 is not yet implemented, STOP — this story depends on it.
  - [ ] 0.3 Read existing DAPR service patterns to understand key derivation and DaprClient usage:
    - `src/Hexalith.EventStore.Server/Commands/DaprCommandStatusStore.cs` — DaprClient.GetStateAsync/SaveStateAsync pattern
    - `src/Hexalith.EventStore.Server/Commands/CommandStatusConstants.cs` — key derivation: `{tenantId}:{correlationId}:status` (to duplicate in `AdminStateStoreKeys`)
    - `src/Hexalith.EventStore.Server/Commands/CommandArchiveConstants.cs` — key derivation: `{tenantId}:{correlationId}:command` (to duplicate in `AdminStateStoreKeys`)
    - `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs` — EventStreamKeyPrefix, MetadataKey, SnapshotKey (public, use directly)
    - `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs` — parallel event reads pattern
    - `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs` — projection state key `projection-state` (to duplicate in `AdminStateStoreKeys`)
  - [ ] 0.4 Read `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` — DI registration pattern
  - [ ] 0.5 Read `Directory.Build.props`, `Directory.Packages.props` — verify DAPR.Client package version (1.16.1 or current)

- [ ] Task 1: Create Admin.Server project (AC: #1, #7)
  - [ ] 1.1 Create `src/Hexalith.EventStore.Admin.Server/Hexalith.EventStore.Admin.Server.csproj`:
    - `<TargetFramework>net10.0</TargetFramework>`
    - `<Description>DAPR-backed admin service implementations for Hexalith.EventStore — shared backend for Web UI, CLI, and MCP</Description>`
    - `<GenerateDocumentationFile>true</GenerateDocumentationFile>`
    - ProjectReferences:
      - `../Hexalith.EventStore.Admin.Abstractions/Hexalith.EventStore.Admin.Abstractions.csproj`
      - `../Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj`
    - PackageReferences (centralized via Directory.Packages.props):
      - `Dapr.Client`
      - `Microsoft.Extensions.DependencyInjection.Abstractions`
      - `Microsoft.Extensions.Options`
      - `Microsoft.Extensions.Logging.Abstractions`
    - NO ProjectReference to Server — key patterns duplicated in `AdminStateStoreKeys` helper
    - NO ASP.NET Core references (controllers are Story 14-3)
    - NO MediatR (no command pipeline in Admin.Server)
  - [ ] 1.2 Create `Helpers/AdminStateStoreKeys.cs` — static helper duplicating Server key patterns:
    ```csharp
    namespace Hexalith.EventStore.Admin.Server.Helpers;

    /// <summary>
    /// State store key derivation for admin reads. These patterns are duplicated from
    /// Server internals (CommandStatusConstants, CommandArchiveConstants, EventReplayProjectionActor)
    /// to avoid coupling Admin.Server to the Server project.
    /// If Server key patterns change, these MUST be updated to match.
    /// </summary>
    public static class AdminStateStoreKeys
    {
        /// <summary>Command status key. Source: Server/Commands/CommandStatusConstants.BuildKey()</summary>
        public static string CommandStatusKey(string tenantId, string correlationId)
            => $"{tenantId}:{correlationId}:status";

        /// <summary>Command archive key. Source: Server/Commands/CommandArchiveConstants.BuildKey()</summary>
        public static string CommandArchiveKey(string tenantId, string correlationId)
            => $"{tenantId}:{correlationId}:command";
    }
    ```
  - [ ] 1.3 Add the project to `Hexalith.EventStore.slnx`
  - [ ] 1.4 Verify `dotnet build Hexalith.EventStore.slnx --configuration Release` succeeds

- [ ] Task 2: Create AdminServerOptions and configuration (AC: #8)
  - [ ] 2.1 Create `Configuration/AdminServerOptions.cs`:
    ```csharp
    namespace Hexalith.EventStore.Admin.Server.Configuration;

    public sealed class AdminServerOptions
    {
        public const string SectionName = "AdminServer";
        public string StateStoreName { get; set; } = "statestore";
        public string CommandApiAppId { get; set; } = "commandapi";
        public string TenantServiceAppId { get; set; } = "tenants";
        public int DefaultPageSize { get; set; } = 100;
        public int MaxTimelineEvents { get; set; } = 1000;
        public int ServiceInvocationTimeoutSeconds { get; set; } = 30;

        /// <summary>ADR-P5: Observability deep-link URLs. Nullable — missing URLs disable buttons gracefully.</summary>
        public string? TraceUrl { get; set; }
        public string? MetricsUrl { get; set; }
        public string? LogsUrl { get; set; }
    }
    ```
  - [ ] 2.2 Create `Configuration/ServiceCollectionExtensions.cs`:
    ```csharp
    namespace Hexalith.EventStore.Admin.Server.Configuration;

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAdminServer(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<AdminServerOptions>(
                configuration.GetSection(AdminServerOptions.SectionName));

            // Auth context — default no-op, Story 14-3 replaces with real implementation
            services.TryAddScoped<IAdminAuthContext, NullAdminAuthContext>();

            // Register all 9 service implementations as scoped
            services.TryAddScoped<IStreamQueryService, DaprStreamQueryService>();
            services.TryAddScoped<IProjectionQueryService, DaprProjectionQueryService>();
            services.TryAddScoped<IProjectionCommandService, DaprProjectionCommandService>();
            services.TryAddScoped<ITypeCatalogService, DaprTypeCatalogService>();
            services.TryAddScoped<IHealthQueryService, DaprHealthQueryService>();
            services.TryAddScoped<IStorageQueryService, DaprStorageQueryService>();
            services.TryAddScoped<IStorageCommandService, DaprStorageCommandService>();
            services.TryAddScoped<IDeadLetterService, DaprDeadLetterService>();
            services.TryAddScoped<ITenantQueryService, DaprTenantQueryService>();

            return services;
        }
    }
    ```
    - Follow existing pattern from `Server/Configuration/ServiceCollectionExtensions.cs`
    - Use `TryAddScoped` to allow override by test doubles
  - [ ] 2.3 Create `Services/IAdminAuthContext.cs` — scoped interface for JWT token access:
    ```csharp
    namespace Hexalith.EventStore.Admin.Server.Services;

    /// <summary>
    /// Provides the caller's authorization context for service-to-service calls.
    /// All InvokeMethodAsync calls to CommandApi/Tenants must forward the JWT token,
    /// or the target service will reject with 401/403.
    /// Story 14-3 registers the ASP.NET Core implementation that extracts the token
    /// from IHttpContextAccessor. For Tier 1 tests, mock this interface.
    /// </summary>
    public interface IAdminAuthContext
    {
        string? GetToken();
        string? GetUserId();
    }
    ```
    - Register in DI: `services.TryAddScoped<IAdminAuthContext, NullAdminAuthContext>()` (no-op default)
    - Create `Services/NullAdminAuthContext.cs` — returns null for both methods (Story 14-3 replaces with real implementation)
    - Each service injects `IAdminAuthContext` and applies the token inline:
      ```csharp
      var request = _daprClient.CreateInvokeMethodRequest(HttpMethod.Get, appId, endpoint);
      var token = _authContext.GetToken();
      if (token is not null)
          request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
      var response = await _daprClient.InvokeMethodAsync<TResponse>(request, ct).ConfigureAwait(false);
      ```

- [ ] Task 3: Implement IStreamQueryService (AC: #2, #3, #6)
  - [ ] 3.1 Create `Services/DaprStreamQueryService.cs`:
    - Constructor: inject `DaprClient`, `IOptions<AdminServerOptions>`, `ILogger<DaprStreamQueryService>`
    - **CRITICAL: Actor state limitation** — Event data (events, metadata, snapshots) is stored in DAPR actor state, which uses a different key namespace (`{actorType}||{actorId}||{stateKey}`). Plain `DaprClient.GetStateAsync` with `AggregateIdentity` keys will NOT find this data. All event/aggregate data reads MUST delegate to CommandApi via `DaprClient.InvokeMethodAsync`.
    - **GetRecentlyActiveStreamsAsync**: Read admin activity index from state store key `admin:stream-activity:{tenantId}` via `DaprClient.GetStateAsync` (this is non-actor state, safe to read directly). If index doesn't exist, return empty `PagedResult<StreamSummary>` and log warning.
    - **GetStreamTimelineAsync**: Delegate to CommandApi → GET `api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/timeline?from={fromSequence}&to={toSequence}`. CommandApi reads actor state internally and returns timeline. **Pagination guard**: enforce `toSequence - fromSequence <= 1000` to prevent OOM on large streams. If range not specified, default to last 100 events.
    - **GetAggregateStateAtPositionAsync**: Delegate to CommandApi → GET `api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/state?at={sequenceNumber}`. CommandApi replays events internally.
    - **DiffAggregateStateAsync**: Delegate to CommandApi → GET `api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/diff?from={fromSequence}&to={toSequence}`. CommandApi computes diff.
    - **TraceCausationChainAsync**: Delegate to CommandApi → GET `api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/causation?at={sequenceNumber}`. CommandApi traces chain.
  - [ ] 3.2 All `InvokeMethodAsync` calls must forward the caller's JWT token (see Task 2.3 helper).
  - [ ] 3.3 Null/error handling: If CommandApi returns 404, return empty result. If CommandApi returns 5xx or is unreachable, return `AdminOperationResult(Success: false)` or empty DTO — never throw.

- [ ] Task 4: Implement IProjectionQueryService and IProjectionCommandService (AC: #2, #3, #4)
  - [ ] 4.1 Create `Services/DaprProjectionQueryService.cs`:
    - **ListProjectionsAsync**: Read projection registry from state store key `admin:projections:{tenantId}`. If no registry exists, return empty list.
    - **GetProjectionDetailAsync**: Delegate to CommandApi → GET `api/v1/admin/projections/{tenantId}/{projectionName}`. Do NOT read actor state keys directly — actor state format varies by state store provider.
    - Map `ProjectionState` → `ProjectionStatus`/`ProjectionDetail` DTOs
  - [ ] 4.2 Create `Services/DaprProjectionCommandService.cs`:
    - All methods delegate to CommandApi via `DaprClient.InvokeMethodAsync<TRequest, AdminOperationResult>(options.CommandApiAppId, endpoint, request)`
    - **PauseProjectionAsync** → POST `api/v1/admin/projections/{name}/pause`
    - **ResumeProjectionAsync** → POST `api/v1/admin/projections/{name}/resume`
    - **ResetProjectionAsync** → POST `api/v1/admin/projections/{name}/reset`
    - **ReplayProjectionAsync** → POST `api/v1/admin/projections/{name}/replay`
    - Catch `RpcException`/`DaprException` and map to `AdminOperationResult(Success: false, Message: ex.Message, ErrorCode: ex.StatusCode.ToString())` — include error detail so callers know *why* it failed (e.g., "Projection OrderSummary is already paused"). Never throw to caller.

- [ ] Task 5: Implement ITypeCatalogService (AC: #2, #3, #6)
  - [ ] 5.1 Create `Services/DaprTypeCatalogService.cs`:
    - **ListEventTypesAsync**: Read type catalog index from state store key `admin:type-catalog:events:{domain}`. The type catalog is populated by the event publication pipeline as events are processed. If not yet populated, return empty list.
    - **ListCommandTypesAsync**: Read from `admin:type-catalog:commands:{domain}`
    - **ListAggregateTypesAsync**: Read from `admin:type-catalog:aggregates:{domain}`
    - All methods filter by domain if provided, return full list if domain is null

- [ ] Task 6: Implement IHealthQueryService (AC: #2, #3)
  - [ ] 6.1 Create `Services/DaprHealthQueryService.cs`:
    - **CRITICAL: Health must work independently of CommandApi.** If CommandApi is down, Admin.Server must still report health status. Health queries use DAPR metadata and direct state store probes — never CommandApi delegation.
    - **GetSystemHealthAsync**: Aggregate health from multiple sources:
      - DAPR metadata endpoint: `DaprClient.GetMetadataAsync()` for sidecar health and component enumeration
      - State store connectivity: attempt `DaprClient.GetStateAsync` with sentinel key `admin:health-check` — success = state store reachable
      - CommandApi reachability: attempt `DaprClient.InvokeMethodAsync(options.CommandApiAppId, "health")` with short timeout (2s) — success = CommandApi is up, failure = mark as degraded (not unhealthy)
      - Map results to `SystemHealthReport` DTO with `OverallStatus`: Healthy (all green), Degraded (CommandApi unreachable but sidecar OK), Unhealthy (sidecar or state store unreachable)
      - Include `ObservabilityLinks` from `AdminServerOptions.TraceUrl`, `MetricsUrl`, `LogsUrl` properties (ADR-P5)
    - **GetDaprComponentStatusAsync**: Use `DaprClient.GetMetadataAsync()` to enumerate DAPR components. Map to `DaprComponentHealth` DTOs. This never touches CommandApi.

- [ ] Task 7: Implement IStorageQueryService and IStorageCommandService (AC: #2, #3, #4)
  - [ ] 7.1 Create `Services/DaprStorageQueryService.cs`:
    - **GetStorageOverviewAsync**: Read storage metrics from state store key `admin:storage-overview:{tenantId}`. This is a projection-maintained index.
    - **GetHotStreamsAsync**: Read from `admin:storage-hot-streams:{tenantId}`. Sorted by event count descending. Limit to requested count.
  - [ ] 7.2 Create `Services/DaprStorageCommandService.cs`:
    - All methods delegate to CommandApi via DAPR service invocation:
    - **TriggerCompactionAsync** → POST `api/v1/admin/storage/compact`
    - **CreateSnapshotAsync** → POST `api/v1/admin/storage/snapshot`
    - **SetSnapshotPolicyAsync** → PUT `api/v1/admin/storage/snapshot-policy`
    - Catch `RpcException`/`DaprException` → `AdminOperationResult(Success: false, Message: ex.Message, ErrorCode: ex.StatusCode.ToString())`

- [ ] Task 8: Implement IDeadLetterService (AC: #2, #3, #4)
  - [ ] 8.1 Create `Services/DaprDeadLetterService.cs`:
    - **ListDeadLettersAsync**: Read from state store key `admin:dead-letters:{tenantId}` with pagination via continuation token. The dead-letter index is maintained by the `DeadLetterPublisher` in Server.
    - **RetryDeadLettersAsync**: Delegate to CommandApi via `DaprClient.InvokeMethodAsync` → POST `api/v1/admin/dead-letters/retry`
    - **SkipDeadLettersAsync**: Delegate → POST `api/v1/admin/dead-letters/skip`
    - **ArchiveDeadLettersAsync**: Delegate → POST `api/v1/admin/dead-letters/archive`

- [ ] Task 9: Implement ITenantQueryService (AC: #2, #5)
  - [ ] 9.1 Create `Services/DaprTenantQueryService.cs`:
    - All methods delegate to the Hexalith.Tenants peer service via DAPR service invocation
    - **ListTenantsAsync** → `DaprClient.InvokeMethodAsync<IReadOnlyList<TenantSummary>>(options.TenantServiceAppId, "api/v1/tenants")`
    - **GetTenantQuotasAsync** → `DaprClient.InvokeMethodAsync<TenantQuotas>(options.TenantServiceAppId, "api/v1/tenants/{tenantId}/quotas")`
    - **CompareTenantUsageAsync** → `DaprClient.InvokeMethodAsync<TenantComparison>(options.TenantServiceAppId, "api/v1/tenants/compare")`
    - If Tenants service is unavailable, return `AdminOperationResult`-style empty/error results gracefully
    - XML doc: `/// <summary>Delegates to Hexalith.Tenants peer service. EventStore does NOT own tenant state.</summary>`

- [ ] Task 10: Create test project (AC: #9)
  - [ ] 10.1 Create `tests/Hexalith.EventStore.Admin.Server.Tests/Hexalith.EventStore.Admin.Server.Tests.csproj`:
    - Follow existing test project pattern from `Hexalith.EventStore.Server.Tests.csproj`
    - PackageReferences: xUnit, Shouldly, NSubstitute, coverlet.collector (centralized versions)
    - ProjectReference: `../../src/Hexalith.EventStore.Admin.Server/Hexalith.EventStore.Admin.Server.csproj`
  - [ ] 10.2 Add test project to `Hexalith.EventStore.slnx`
  - [ ] 10.3 Write `Services/DaprStreamQueryServiceTests.cs`:
    - Mock `DaprClient` with NSubstitute: `var daprClient = Substitute.For<DaprClient>();`
    - Test `GetStreamTimelineAsync`: mock returns `AggregateMetadata` for metadata key, mock returns `EventEnvelope` for event keys → verify correct DTO mapping
    - Test null metadata: mock returns null → verify empty `StreamTimeline` returned
    - Test key derivation: verify `DaprClient.GetStateAsync` called with exact keys matching `AggregateIdentity` patterns
  - [ ] 10.4 Write `Services/DaprProjectionQueryServiceTests.cs`:
    - Test `ListProjectionsAsync`: mock returns projection list → verify DTO mapping
    - Test empty registry: mock returns null → verify empty list returned
  - [ ] 10.5 Write `Services/DaprProjectionCommandServiceTests.cs`:
    - Test write delegation: verify `DaprClient.InvokeMethodAsync` called with correct CommandApi app ID and endpoint
    - Test error handling: mock throws `RpcException` → verify `AdminOperationResult(Success: false)` returned
  - [ ] 10.6 Write `Services/DaprHealthQueryServiceTests.cs`:
    - Test `GetSystemHealthAsync`: mock returns metadata → verify `SystemHealthReport` mapping
  - [ ] 10.7 Write `Services/DaprTenantQueryServiceTests.cs`:
    - Test delegation: verify `DaprClient.InvokeMethodAsync` called with correct Tenants service app ID
    - Test service unavailable: mock throws → verify graceful empty result
  - [ ] 10.8 Write `Services/DaprStorageServiceTests.cs` (combined query+command):
    - Test `GetStorageOverviewAsync`: mock returns storage overview → verify DTO mapping
    - Test empty storage: mock returns null → verify empty `StorageOverview` returned
    - Test write delegation: verify `DaprClient.InvokeMethodAsync` called with correct CommandApi endpoints for compaction/snapshot/policy
    - Test timeout: mock throws `RpcException` → verify `AdminOperationResult(Success: false)`
  - [ ] 10.9 Write `Services/DaprDeadLetterServiceTests.cs`:
    - Test `ListDeadLettersAsync`: mock returns dead letter list → verify DTO mapping and pagination
    - Test empty list: mock returns null → verify empty `PagedResult<DeadLetterEntry>`
    - Test retry/skip/archive delegation: verify `DaprClient.InvokeMethodAsync` called with correct endpoints
  - [ ] 10.10 Write `Services/DaprTypeCatalogServiceTests.cs`:
    - Test `ListEventTypesAsync`: mock returns type catalog → verify DTO mapping
    - Test domain filtering: verify correct state store key includes domain parameter
    - Test empty catalog: mock returns null → verify empty list returned
  - [ ] 10.11 Write `Helpers/AdminStateStoreKeysTests.cs`:
    - Test `CommandStatusKey`: verify output matches `{tenantId}:{correlationId}:status` format
    - Test `CommandArchiveKey`: verify output matches `{tenantId}:{correlationId}:command` format
  - [ ] 10.12 Write `Configuration/ServiceCollectionExtensionsTests.cs`:
    - Test `AddAdminServer()`: verify all 9 services registered
    - Test options binding: verify `AdminServerOptions` bound from configuration

- [ ] Task 11: Build and test (AC: all)
  - [ ] 11.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings
  - [ ] 11.2 `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/` — all pass
  - [ ] 11.3 Run all existing Tier 1 tests — 0 regressions

## Dev Notes

### Architecture: Hybrid Data Access (ADR-P4)

Admin.Server is the **single backend** consumed by three interfaces:
- **Web UI** (in-process Blazor) — Story 14-3 adds REST controllers, Story 15 adds Blazor UI
- **CLI** (HTTP client) — Epic 17
- **MCP** (HTTP client) — Epic 18

**Core rule:** Everything delegates to CommandApi via `DaprClient.InvokeMethodAsync` except (1) health checks (use `DaprClient.GetMetadataAsync` directly — must work when CommandApi is down) and (2) admin index reads (use `DaprClient.GetStateAsync` — non-actor state written by projections).

**JWT token forwarding**: ALL `InvokeMethodAsync` calls to CommandApi or Tenants must propagate the caller's JWT token via `IAdminAuthContext.GetToken()` + Authorization header, or the target service will reject with 401/403.

**Timeout**: ALL `InvokeMethodAsync` calls must use `CancellationTokenSource.CreateLinkedTokenSource(ct)` with `AdminServerOptions.ServiceInvocationTimeoutSeconds` (default 30s) to prevent hanging on unresponsive services.

Admin.Server NEVER writes to the event store state store directly. All mutations go through the command pipeline to preserve validation, authorization, and event sourcing guarantees.

### Actor State Store Limitation (CRITICAL)

The event store's core data (events, metadata, snapshots) is written by `AggregateActor` using `IActorStateManager`. DAPR stores actor state in a separate key namespace from regular state: the internal key format is `{actorType}||{actorId}||{userKey}`, which varies by state store provider.

**Consequence:** `DaprClient.GetStateAsync("statestore", identity.MetadataKey)` will return null even when the data exists — because the AggregateIdentity-derived key (`tenant:domain:aggId:metadata`) is the *user key*, not the full actor state key.

**Solution adopted:** Delegate event data reads to CommandApi, which hosts the actors and can read their state via `IActorStateManager`. CommandApi admin read endpoints are required (e.g., `GET api/v1/admin/streams/{tenant}/{domain}/{aggId}/timeline`). If these endpoints don't exist yet on CommandApi, the Admin.Server service methods will receive 404 and return empty results gracefully.

### CommandApi Admin Endpoints Prerequisite (CONTRACT-FIRST)

This story implements Admin.Server services that call CommandApi admin read/write endpoints. **These endpoints do not yet exist on CommandApi.** This is intentional — Story 14-2 is a contract-first implementation:

- **Tier 1 tests pass** with mocked `DaprClient` — they verify correct endpoint construction, error handling, and DTO mapping
- **Real integration requires** CommandApi to expose the admin endpoints listed below
- **Story 14-3** (REST API controllers) adds controllers to Admin.Server — but CommandApi endpoints are a separate concern

**CommandApi admin endpoints expected by this story:**

| Method | Endpoint | Used By |
|--------|----------|---------|
| GET | `api/v1/admin/streams/{tenant}/{domain}/{aggId}/timeline?from=&to=` | DaprStreamQueryService |
| GET | `api/v1/admin/streams/{tenant}/{domain}/{aggId}/state?at=` | DaprStreamQueryService |
| GET | `api/v1/admin/streams/{tenant}/{domain}/{aggId}/diff?from=&to=` | DaprStreamQueryService |
| GET | `api/v1/admin/streams/{tenant}/{domain}/{aggId}/causation?at=` | DaprStreamQueryService |
| GET | `api/v1/admin/projections/{tenant}/{name}` | DaprProjectionQueryService |
| POST | `api/v1/admin/projections/{name}/pause` | DaprProjectionCommandService |
| POST | `api/v1/admin/projections/{name}/resume` | DaprProjectionCommandService |
| POST | `api/v1/admin/projections/{name}/reset` | DaprProjectionCommandService |
| POST | `api/v1/admin/projections/{name}/replay` | DaprProjectionCommandService |
| POST | `api/v1/admin/storage/compact` | DaprStorageCommandService |
| POST | `api/v1/admin/storage/snapshot` | DaprStorageCommandService |
| PUT | `api/v1/admin/storage/snapshot-policy` | DaprStorageCommandService |
| POST | `api/v1/admin/dead-letters/retry` | DaprDeadLetterService |
| POST | `api/v1/admin/dead-letters/skip` | DaprDeadLetterService |
| POST | `api/v1/admin/dead-letters/archive` | DaprDeadLetterService |

These endpoints should be added to CommandApi as a prerequisite task or as part of a follow-up story before Tier 2/3 integration testing.

### State Store Key Derivation (CRITICAL)

Admin.Server must use **identical** key derivation to the Server project. Keys come from two sources:

**From Contracts (public, via AggregateIdentity):**
| Data | Key Pattern | Source Method |
|------|------------|---------------|
| Events | `{tenant}:{domain}:{aggId}:events:{seq}` | `AggregateIdentity.EventStreamKeyPrefix + seq` |
| Metadata | `{tenant}:{domain}:{aggId}:metadata` | `AggregateIdentity.MetadataKey` |
| Snapshot | `{tenant}:{domain}:{aggId}:snapshot` | `AggregateIdentity.SnapshotKey` |

**From AdminStateStoreKeys helper (duplicated from Server internals):**
| Data | Key Pattern | Source Helper Method |
|------|------------|---------------------|
| Command status | `{tenant}:{correlationId}:status` | `AdminStateStoreKeys.CommandStatusKey()` |
| Command archive | `{tenant}:{correlationId}:command` | `AdminStateStoreKeys.CommandArchiveKey()` |

### DaprClient Usage Patterns

Follow the existing pattern from `DaprCommandStatusStore.cs`:

```csharp
// Read — returns default(T) when key not found
T? result = await _daprClient.GetStateAsync<T>(
    _options.StateStoreName,
    key,
    cancellationToken: cancellationToken).ConfigureAwait(false);

// Write delegation via service invocation
TResponse response = await _daprClient.InvokeMethodAsync<TRequest, TResponse>(
    appId,
    methodName,
    request,
    cancellationToken: cancellationToken).ConfigureAwait(false);
```

**Null handling**: `GetStateAsync` returns `default(T)` (null for reference types) when a key doesn't exist. Always check for null before mapping. Return empty collections or `AdminOperationResult(Success: false, Message: "Not found")` — never throw.

**ConfigureAwait(false)**: ALL async calls must use `.ConfigureAwait(false)` per project convention.

### Admin Index Keys

Several services depend on admin-specific index keys (e.g., `admin:stream-activity:{tenantId}`, `admin:type-catalog:events:{domain}`). These indexes are NOT yet populated by the event pipeline — they will be maintained by future projection actors. For now:
- Services reading these indexes should return empty results when the key doesn't exist
- Log a warning: `"Admin index '{indexKey}' not found. Index population requires admin projection setup."`
- This allows the Admin.Server to compile and pass tests now, with full functionality added as projections are built

### Dependencies

```
Admin.Server → Admin.Abstractions → Contracts (only)
Admin.Server → Dapr.Client (DaprClient for state store, service invocation, and actor invocation)
Admin.Server → Microsoft.Extensions.DependencyInjection.Abstractions
Admin.Server → Microsoft.Extensions.Options
Admin.Server → Microsoft.Extensions.Logging.Abstractions
```

No dependency on Server project. Key patterns duplicated in `Helpers/AdminStateStoreKeys.cs` with XML doc linking to Server source.

### DO NOT

- Do NOT add ASP.NET Core controller dependencies — controllers are Story 14-3
- Do NOT add `[ApiController]`, `[Route]`, or `[Authorize]` attributes — those are Story 14-3
- Do NOT add MediatR — Admin.Server reads state directly, it doesn't process commands
- Do NOT write to the state store — all writes go through CommandApi
- Do NOT use `DaprClient.QueryStateAsync()` — not all state store backends support it (NFR44)
- Do NOT use `IActorStateManager` — Admin.Server is not an actor, it reads state store directly via DaprClient
- Do NOT add a ProjectReference to Server — key patterns are duplicated in `Helpers/AdminStateStoreKeys.cs` to avoid coupling
- Do NOT read DAPR actor state store keys directly via `DaprClient.GetStateAsync` — use `DaprClient.InvokeActorMethodAsync` for actor state (key format varies by state store provider)

### Naming and Organization Conventions

Follow existing patterns exactly:
- **Namespace**: `Hexalith.EventStore.Admin.Server.Services` for implementations, `Hexalith.EventStore.Admin.Server.Configuration` for DI/options
- **Service naming**: `Dapr{Domain}Service.cs` (e.g., `DaprStreamQueryService.cs`) — prefix with `Dapr` to indicate the backing infrastructure
- **File-scoped namespaces**: `namespace X.Y.Z;`
- **One public type per file**: File name = type name
- **Braces**: Allman style (opening brace on new line)
- **Private fields**: `_camelCase` (e.g., `_daprClient`, `_options`, `_logger`)
- **Async methods**: `Async` suffix, all accept `CancellationToken`, all use `.ConfigureAwait(false)`
- **XML documentation**: On every public method
- **Indentation**: 4 spaces, CRLF line endings, UTF-8

### Performance Budget (NFR40)

- Read operations: < 500ms at p99
- Write operations: < 2s at p99
- Use pagination (DefaultPageSize = 100) for list operations
- **Timeline guard**: Enforce `MaxTimelineEvents = 1000` on `GetStreamTimelineAsync` to prevent OOM on aggregates with 50K+ events. If no range specified, return last 100 events only.

### Test Conventions

- **Framework**: xUnit 2.9.3
- **Assertions**: Shouldly 4.3.0 (`result.ShouldNotBeNull()`, `result.Items.Count.ShouldBe(5)`)
- **Mocking**: NSubstitute 5.3.0 — `DaprClient` is an abstract class (not interface), NSubstitute can mock it: `Substitute.For<DaprClient>()`. When verifying `Received()` calls, match the exact `GetStateAsync<T>` overload signature or assertions will fail silently.
- **Coverage**: coverlet.collector
- **Pattern**: One test class per service, test methods named `{Method}_{Scenario}_{ExpectedResult}`
- Mock DaprClient — do NOT start real DAPR for Tier 1 tests
- Test null/timeout scenarios for `InvokeMethodAsync` — network failures are the #1 production failure mode for write delegation
- **Tier 2 note (future)**: Once CommandApi admin read endpoints exist, add contract tests that verify serialization round-trip compatibility between Admin.Server DTOs and CommandApi response formats. Use shared fixtures. This is NOT part of the current story but should be tracked.

### Previous Story Intelligence (14-1)

Story 14-1 created `Admin.Abstractions` with:
- 9 service interfaces in `Services/` namespace
- DTOs in `Models/{Feature}/` namespaces organized by domain
- Shared types: `PagedResult<T>`, `AdminOperationResult`, `AdminRole` enum
- `ToString()` redaction on sensitive DTOs (SEC-5)
- Records with inline validation (`ArgumentException` on null/empty required fields)

Admin.Server MUST match the exact interface signatures from Admin.Abstractions. Do not add parameters or change return types.

### Project Structure

```
src/Hexalith.EventStore.Admin.Server/
  Hexalith.EventStore.Admin.Server.csproj
  Configuration/
    AdminServerOptions.cs
    ServiceCollectionExtensions.cs
  Helpers/
    AdminStateStoreKeys.cs
  Services/
    IAdminAuthContext.cs
    NullAdminAuthContext.cs
    DaprStreamQueryService.cs
    DaprProjectionQueryService.cs
    DaprProjectionCommandService.cs
    DaprTypeCatalogService.cs
    DaprHealthQueryService.cs
    DaprStorageQueryService.cs
    DaprStorageCommandService.cs
    DaprDeadLetterService.cs
    DaprTenantQueryService.cs

tests/Hexalith.EventStore.Admin.Server.Tests/
  Hexalith.EventStore.Admin.Server.Tests.csproj
  Configuration/
    ServiceCollectionExtensionsTests.cs
  Helpers/
    AdminStateStoreKeysTests.cs
  Services/
    DaprStreamQueryServiceTests.cs
    DaprProjectionQueryServiceTests.cs
    DaprProjectionCommandServiceTests.cs
    DaprHealthQueryServiceTests.cs
    DaprStorageServiceTests.cs
    DaprDeadLetterServiceTests.cs
    DaprTypeCatalogServiceTests.cs
    DaprTenantQueryServiceTests.cs
```

### References

- [Source: _bmad-output/planning-artifacts/architecture.md — ADR-P4: Admin Tooling Three-Interface Architecture]
- [Source: _bmad-output/planning-artifacts/architecture.md — ADR-P5: Observability Deep-Link Strategy]
- [Source: _bmad-output/planning-artifacts/prd.md — FR79, FR82, NFR40, NFR44, NFR46]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-03-21-admin-tooling.md — Epic 14 stories]
- [Source: _bmad-output/implementation-artifacts/14-1-admin-abstractions-service-interfaces-and-dtos.md — Interface definitions]
- [Source: src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs — Key derivation methods]
- [Source: src/Hexalith.EventStore.Server/Commands/DaprCommandStatusStore.cs — DaprClient usage pattern]
- [Source: src/Hexalith.EventStore.Server/Commands/CommandStatusConstants.cs — Status key derivation]
- [Source: src/Hexalith.EventStore.Server/Commands/CommandArchiveConstants.cs — Archive key derivation]
- [Source: src/Hexalith.EventStore.Server/Events/EventStreamReader.cs — Parallel event read pattern]
- [Source: src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs — Projection state key]
- [Source: src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs — DI registration pattern]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
