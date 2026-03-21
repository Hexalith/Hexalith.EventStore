# Story 14.3: Admin.Server — REST API Controllers with JWT Auth

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer building admin tooling (Web UI, CLI, or MCP),
I want REST API controllers in Admin.Server that expose all admin service interfaces over HTTP with JWT authentication and role-based authorization,
So that CLI and MCP clients can consume the Admin API over HTTP, and the Web UI can call the same endpoints, all with consistent security enforcement.

## Acceptance Criteria

1. **Given** Admin.Server, **When** built, **Then** it contains ASP.NET Core controllers exposing all 10 admin service interfaces as REST endpoints under the `api/v1/admin/` route prefix, compiles with zero errors/warnings.
2. **Given** Admin.Server, **When** its controllers are inspected, **Then** every controller method delegates to the corresponding service interface method — controllers contain zero business logic.
3. **Given** any admin API endpoint, **When** called without a valid JWT Bearer token, **Then** a 401 Unauthorized response is returned with RFC 7807 `ProblemDetails` format.
4. **Given** a read-only endpoint (streams, projections list, type catalog, health, storage overview, dead-letter list), **When** called with a valid JWT token bearing any `AdminRole` (ReadOnly, Operator, or Admin), **Then** the request is authorized.
5. **Given** an operator-level endpoint (projection pause/resume/reset/replay, compaction, snapshot creation, snapshot policy, dead-letter retry/skip/archive), **When** called with a JWT token bearing only `ReadOnly` role, **Then** a 403 Forbidden response is returned. **When** called with `Operator` or `Admin` role, **Then** the request is authorized.
6. **Given** an admin-level endpoint (tenant quota management, tenant comparison), **When** called with a JWT token bearing `ReadOnly` or `Operator` role, **Then** a 403 Forbidden response is returned. **When** called with `Admin` role, **Then** the request is authorized.
7. **Given** any endpoint receiving a tenant-scoped request, **When** the JWT token's `eventstore:tenant` claims do not include the requested tenant, **Then** a 403 Forbidden response is returned (SEC-3 tenant isolation).
8. **Given** any endpoint, **When** the underlying service throws or returns a failure, **Then** the controller returns appropriate HTTP status codes (404 for not found, 503 for DAPR/service unavailability, 500 for unexpected errors) with `ProblemDetails` format including `correlationId` extension — never stack traces (Enforcement #13).
9. **Given** `ServiceCollectionExtensions.AddAdminApi()`, **When** called, **Then** it registers authorization policies for the three admin roles, admin claims transformation, tenant authorization filter, and admin services. Authentication and controller registration are the host's responsibility (Story 14-4).
10. **Given** a new Tier 1 test project `tests/Hexalith.EventStore.Admin.Server.Tests/` (or extending the existing one from Story 14-2), **When** tests run, **Then** controller tests validate: correct service delegation, authorization policy enforcement, tenant isolation, error mapping to ProblemDetails, and correlation ID propagation.

## Tasks / Subtasks

- [ ] Task 0: Prerequisites (AC: all)
  - [ ] 0.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` — confirm baseline compiles (0 errors, 0 warnings)
  - [ ] 0.2 Verify Story 14-1 output: `src/Hexalith.EventStore.Admin.Abstractions/` exists with all 10 service interfaces. If not, STOP.
  - [ ] 0.2b Verify Story 14-2 output: `src/Hexalith.EventStore.Admin.Server/` exists with `AddAdminServer()` in `Configuration/ServiceCollectionExtensions.cs` and all 9 service implementations. If not, STOP — this story depends on 14-2.
  - [ ] 0.3 Read existing controller patterns for conventions:
    - `src/Hexalith.EventStore.CommandApi/Controllers/CommandsController.cs` — constructor injection, `[ApiController]`, `[Authorize]`, route pattern, `ControllerBase` base class, ProblemDetails error responses
    - `src/Hexalith.EventStore.CommandApi/Controllers/CommandStatusController.cs` — tenant claim extraction via `User.FindAll("eventstore:tenant")`, correlation ID from `HttpContext`
    - `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs` — JWT auth registration, exception handler ordering, OpenAPI setup
    - `src/Hexalith.EventStore.CommandApi/Hexalith.EventStore.CommandApi.csproj` — package references for ASP.NET Core
  - [ ] 0.4 Read `src/Hexalith.EventStore.Admin.Abstractions/Models/Common/AdminRole.cs` — verify enum values: ReadOnly, Operator, Admin
  - [ ] 0.5 Read `src/Hexalith.EventStore.Admin.Server/Configuration/ServiceCollectionExtensions.cs` — understand existing `AddAdminServer()` registration (from Story 14-2)
  - [ ] 0.6 Read `Directory.Build.props`, `Directory.Packages.props` — confirm available package versions

- [ ] Task 1: Add ASP.NET Core dependencies to Admin.Server (AC: #1)
  - [ ] 1.1 Update `src/Hexalith.EventStore.Admin.Server/Hexalith.EventStore.Admin.Server.csproj`:
    - Add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to enable controller base classes, authorization, and claims transformation in a class library. This FrameworkReference includes JWT Bearer auth — do NOT add a separate `PackageReference` for `Microsoft.AspNetCore.Authentication.JwtBearer` (causes version conflicts)
    - Keep existing dependencies from Story 14-2 intact
    - Do NOT convert to an executable — Admin.Server remains a class library; the host is created in Story 14-4
  - [ ] 1.2 Verify `dotnet build Hexalith.EventStore.slnx --configuration Release` succeeds

- [ ] Task 2: Create authorization infrastructure (AC: #4, #5, #6, #7)
  - [ ] 2.1 Create `Authorization/AdminAuthorizationPolicies.cs`:
    ```csharp
    namespace Hexalith.EventStore.Admin.Server.Authorization;

    /// <summary>
    /// Defines authorization policy names for admin API endpoints (NFR46).
    /// </summary>
    public static class AdminAuthorizationPolicies
    {
        /// <summary>ReadOnly: stream browsing, state inspection, type catalog, health.</summary>
        public const string ReadOnly = "AdminReadOnly";

        /// <summary>Operator: ReadOnly + projection controls, snapshots, compaction, dead-letters.</summary>
        public const string Operator = "AdminOperator";

        /// <summary>Admin: Operator + tenant management, backup/restore.</summary>
        public const string Admin = "AdminFull";
    }
    ```
  - [ ] 2.2 Create `Authorization/AdminClaimTypes.cs`:
    ```csharp
    namespace Hexalith.EventStore.Admin.Server.Authorization;

    /// <summary>
    /// Admin-specific JWT claim types.
    /// </summary>
    public static class AdminClaimTypes
    {
        /// <summary>Claim containing the user's admin role (ReadOnly, Operator, Admin).</summary>
        public const string AdminRole = "eventstore:admin-role";

        /// <summary>Reuse existing tenant claim from CommandApi.</summary>
        public const string Tenant = "eventstore:tenant";
    }
    ```
  - [ ] 2.3 Create `Authorization/AdminTenantAuthorizationFilter.cs` — action filter that extracts the `tenantId` route parameter and validates it against the caller's `eventstore:tenant` claims (SEC-3 tenant isolation). Returns 403 if the caller lacks the requested tenant claim. Skip validation if `tenantId` route param is absent (tenant-agnostic endpoints like health, type catalog).
  - [ ] 2.4 Create `Authorization/AdminClaimsTransformation.cs` — `IClaimsTransformation` that inspects existing JWT claims and adds the `eventstore:admin-role` claim based on **existing claim patterns already used in CommandApi**:
    - Has `global_admin`, `is_global_admin`, or role claim containing `GlobalAdministrator`/`global-administrator`/`global-admin` (same detection as `CommandsController.cs`) → adds `eventstore:admin-role` = `Admin`
    - Has `eventstore:permission` = `command:replay` → adds `eventstore:admin-role` = `Operator` (only DBA/operator users have replay permission — this prevents privilege escalation where a regular `command:submit` user could pause projections or trigger compaction)
    - Any authenticated user with valid `eventstore:tenant` claim → adds `eventstore:admin-role` = `ReadOnly`
    - No admin-relevant claims → no claim added (all admin endpoints return 403 with detail: "Admin role required. Ensure JWT contains eventstore:tenant or eventstore:permission claims.")
    - NOTE: The Operator mapping intentionally requires `command:replay` specifically, NOT any `eventstore:permission`. A user with only `command:submit` + `command:query` gets ReadOnly — they can browse streams and inspect state but cannot control projections, trigger compaction, or manage dead-letters. This is a deliberate security boundary.
    - This approach works with any OIDC provider (not Keycloak-specific) and maps from claims that ALREADY exist in the codebase — no invented permission values
    - CRITICAL: The transformation MUST be idempotent — check `identity.HasClaim(c => c.Type == AdminClaimTypes.AdminRole)` before adding. ASP.NET Core calls `TransformAsync` on every request, not just the first. Without this guard, duplicate claims accumulate.
    - CRITICAL: The transformation MUST be exception-safe — wrap all claim inspection in try/catch. If transformation throws, every authenticated request returns 500. On failure: log warning, return the identity unchanged (fail-open to let authorization policies handle denial).
    - Co-hosted scenario: If Admin.Server runs in the same host as CommandApi, both `EventStoreClaimsTransformation` (CommandApi) and `AdminClaimsTransformation` (Admin) will be registered. Admin transformation reads `eventstore:permission` claims — if CommandApi's transformation creates/modifies these, Admin transformation must run after. Register Admin transformation AFTER calling `AddCommandApi()` in the host.

- [ ] Task 3: Create stream controller (AC: #1, #2, #4, #7)
  - [ ] 3.1 Create `Controllers/AdminStreamsController.cs`:
    ```
    [ApiController]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [Route("api/v1/admin/streams")]
    [Tags("Admin - Streams")]
    ```
    - Constructor: inject `IStreamQueryService`, `ILogger<AdminStreamsController>`
    - **GET** `/` → `GetRecentlyActiveStreams(tenantId?, domain?, count?)` — delegates to `GetRecentlyActiveStreamsAsync`
    - **GET** `/{tenantId}/{domain}/{aggregateId}/timeline` → `GetStreamTimeline(tenantId, domain, aggregateId, fromSequence?, toSequence?, count?)` — delegates to `GetStreamTimelineAsync`
    - **GET** `/{tenantId}/{domain}/{aggregateId}/state` → `GetAggregateState(tenantId, domain, aggregateId, sequenceNumber)` — delegates to `GetAggregateStateAtPositionAsync`
    - **GET** `/{tenantId}/{domain}/{aggregateId}/diff` → `DiffAggregateState(tenantId, domain, aggregateId, fromSequence, toSequence)` — delegates to `DiffAggregateStateAsync`
    - **GET** `/{tenantId}/{domain}/{aggregateId}/events/{sequenceNumber}` → `GetEventDetail(...)` — delegates to `GetEventDetailAsync`
    - **GET** `/{tenantId}/{domain}/{aggregateId}/causation` → `TraceCausationChain(tenantId, domain, aggregateId, sequenceNumber)` — delegates to `TraceCausationChainAsync`
    - Apply `[ServiceFilter(typeof(AdminTenantAuthorizationFilter))]` on tenant-scoped endpoints
    - Return `ProblemDetails` on null results (404)

- [ ] Task 4: Create projection controller (AC: #1, #2, #4, #5, #7)
  - [ ] 4.1 Create `Controllers/AdminProjectionsController.cs`:
    ```
    [ApiController]
    [Authorize]
    [Route("api/v1/admin/projections")]
    [Tags("Admin - Projections")]
    ```
    - **GET** `/` → `ListProjections(tenantId?)` — `[Authorize(Policy = ReadOnly)]`, delegates to `IProjectionQueryService.ListProjectionsAsync`
    - **GET** `/{tenantId}/{projectionName}` → `GetProjectionDetail(...)` — `[Authorize(Policy = ReadOnly)]`, delegates to `GetProjectionDetailAsync`
    - **POST** `/{tenantId}/{projectionName}/pause` → `PauseProjection(...)` — `[Authorize(Policy = Operator)]`, delegates to `IProjectionCommandService.PauseProjectionAsync`
    - **POST** `/{tenantId}/{projectionName}/resume` → `ResumeProjection(...)` — `[Authorize(Policy = Operator)]`
    - **POST** `/{tenantId}/{projectionName}/reset` → `ResetProjection(tenantId, projectionName, fromPosition?)` — `[Authorize(Policy = Operator)]`
    - **POST** `/{tenantId}/{projectionName}/replay` → `ReplayProjection(tenantId, projectionName, fromPosition, toPosition)` — `[Authorize(Policy = Operator)]`

- [ ] Task 5: Create type catalog controller (AC: #1, #2, #4)
  - [ ] 5.1 Create `Controllers/AdminTypeCatalogController.cs`:
    ```
    [ApiController]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [Route("api/v1/admin/types")]
    [Tags("Admin - Type Catalog")]
    ```
    - **GET** `/events` → `ListEventTypes(domain?)` — delegates to `ITypeCatalogService.ListEventTypesAsync`
    - **GET** `/commands` → `ListCommandTypes(domain?)` — delegates to `ListCommandTypesAsync`
    - **GET** `/aggregates` → `ListAggregateTypes(domain?)` — delegates to `ListAggregateTypesAsync`
    - No tenant scoping — type catalog is tenant-agnostic per interface XML doc

- [ ] Task 6: Create health controller (AC: #1, #2, #4)
  - [ ] 6.1 Create `Controllers/AdminHealthController.cs`:
    ```
    [ApiController]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [Route("api/v1/admin/health")]
    [Tags("Admin - Health")]
    ```
    - **GET** `/` → `GetSystemHealth()` — delegates to `IHealthQueryService.GetSystemHealthAsync`
    - **GET** `/dapr` → `GetDaprComponentStatus()` — delegates to `GetDaprComponentStatusAsync`
    - No tenant scoping — health is system-wide

- [ ] Task 7: Create storage controller (AC: #1, #2, #4, #5, #7)
  - [ ] 7.1 Create `Controllers/AdminStorageController.cs`:
    ```
    [ApiController]
    [Authorize]
    [Route("api/v1/admin/storage")]
    [Tags("Admin - Storage")]
    ```
    - **GET** `/overview` → `GetStorageOverview(tenantId?)` — `[Authorize(Policy = ReadOnly)]`, delegates to `IStorageQueryService.GetStorageOverviewAsync`
    - **GET** `/hot-streams` → `GetHotStreams(tenantId?, count?)` — `[Authorize(Policy = ReadOnly)]`
    - **GET** `/snapshot-policies` → `GetSnapshotPolicies(tenantId?)` — `[Authorize(Policy = ReadOnly)]`
    - **POST** `/{tenantId}/compact` → `TriggerCompaction(tenantId, domain?)` — `[Authorize(Policy = Operator)]`, delegates to `IStorageCommandService.TriggerCompactionAsync`
    - **POST** `/{tenantId}/{domain}/{aggregateId}/snapshot` → `CreateSnapshot(...)` — `[Authorize(Policy = Operator)]`
    - **PUT** `/{tenantId}/{domain}/{aggregateType}/snapshot-policy` → `SetSnapshotPolicy(tenantId, domain, aggregateType, intervalEvents)` — `[Authorize(Policy = Operator)]`

- [ ] Task 8: Create dead-letter controller (AC: #1, #2, #4, #5, #7)
  - [ ] 8.1 Create `Controllers/AdminDeadLettersController.cs`:
    ```
    [ApiController]
    [Authorize]
    [Route("api/v1/admin/dead-letters")]
    [Tags("Admin - Dead Letters")]
    ```
    - Constructor: inject `IDeadLetterQueryService`, `IDeadLetterCommandService`, `ILogger<AdminDeadLettersController>` (CQRS-split — two separate interfaces, NOT a single `IDeadLetterService`)
    - **GET** `/` → `ListDeadLetters(tenantId?, count?, continuationToken?)` — `[Authorize(Policy = ReadOnly)]`, delegates to `IDeadLetterQueryService.ListDeadLettersAsync`
    - **POST** `/{tenantId}/retry` → `RetryDeadLetters(tenantId, messageIds)` — `[Authorize(Policy = Operator)]`, delegates to `IDeadLetterCommandService.RetryDeadLettersAsync`, request body: `DeadLetterActionRequest { IReadOnlyList<string> MessageIds }`
    - **POST** `/{tenantId}/skip` → `SkipDeadLetters(...)` — `[Authorize(Policy = Operator)]`
    - **POST** `/{tenantId}/archive` → `ArchiveDeadLetters(...)` — `[Authorize(Policy = Operator)]`

- [ ] Task 9: Create tenant controller (AC: #1, #2, #6)
  - [ ] 9.1 Create `Controllers/AdminTenantsController.cs`:
    ```
    [ApiController]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [Route("api/v1/admin/tenants")]
    [Tags("Admin - Tenants")]
    ```
    - **GET** `/` → `ListTenants()` — delegates to `ITenantQueryService.ListTenantsAsync`
    - **GET** `/{tenantId}/quotas` → `GetTenantQuotas(tenantId)` — delegates to `GetTenantQuotasAsync`
    - **POST** `/compare` → `CompareTenantUsage(tenantIds)` — request body: `TenantCompareRequest { IReadOnlyList<string> TenantIds }`

- [ ] Task 10: Create request/response DTOs for controller actions (AC: #2)
  - [ ] 10.1 Create `Models/DeadLetterActionRequest.cs` — record: `MessageIds (IReadOnlyList<string>)` with validation
  - [ ] 10.2 Create `Models/TenantCompareRequest.cs` — record: `TenantIds (IReadOnlyList<string>)` with validation
  - [ ] 10.3 Create `Models/ProjectionReplayRequest.cs` — record: `FromPosition (long), ToPosition (long)` for replay body
  - [ ] 10.4 Create `Models/ProjectionResetRequest.cs` — record: `FromPosition (long?)` for reset body

- [ ] Task 11: Update ServiceCollectionExtensions (AC: #9)
  - [ ] 11.1 Add `AddAdminApi()` extension method to `Configuration/ServiceCollectionExtensions.cs`:
    ```csharp
    public static IServiceCollection AddAdminApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Authorization policies (NFR46)
        services.AddAuthorizationBuilder()
            .AddPolicy(AdminAuthorizationPolicies.ReadOnly, policy =>
                policy.RequireClaim(AdminClaimTypes.AdminRole))
            .AddPolicy(AdminAuthorizationPolicies.Operator, policy =>
                policy.RequireClaim(AdminClaimTypes.AdminRole, "Operator", "Admin"))
            .AddPolicy(AdminAuthorizationPolicies.Admin, policy =>
                policy.RequireClaim(AdminClaimTypes.AdminRole, "Admin"));

        // 2. Admin claims transformation (maps existing claims to admin roles)
        services.AddTransient<IClaimsTransformation, AdminClaimsTransformation>();

        // 3. Tenant authorization filter
        services.AddScoped<AdminTenantAuthorizationFilter>();

        // 4. Register admin services (from Story 14-2)
        services.AddAdminServer(configuration);

        // NOTE: The host (Story 14-4) MUST call:
        //   builder.Services.AddAuthentication(...).AddJwtBearer();
        //   builder.Services.AddControllers()
        //       .AddApplicationPart(typeof(AdminStreamsController).Assembly);
        // Admin.Server does NOT register auth or controllers — the host owns those.

        return services;
    }
    ```
    - IMPORTANT: `AddAdminApi()` calls `AddAdminServer()` internally — consumers only need to call `AddAdminApi()` plus host-level auth/controller registration
    - `AddAdminApi()` does NOT call `AddAuthentication()` or `AddControllers()` — these are host responsibilities to avoid double-registration when cohosted with CommandApi

- [ ] Task 12: Create test project or extend existing (AC: #10)
  - [ ] 12.1 If `tests/Hexalith.EventStore.Admin.Server.Tests/` already exists from Story 14-2, add controller tests to it. Otherwise, create the project:
    - PackageReferences: xUnit, Shouldly, NSubstitute, coverlet.collector, `Microsoft.AspNetCore.Mvc.Testing` (for `WebApplicationFactory` integration tests)
    - ProjectReference: `../../src/Hexalith.EventStore.Admin.Server/Hexalith.EventStore.Admin.Server.csproj`
  - [ ] 12.2 Write `Controllers/AdminStreamsControllerTests.cs`:
    - Mock `IStreamQueryService` with NSubstitute
    - Test `GetRecentlyActiveStreams`: verify service called with correct parameters, verify 200 response
    - Test null result: verify 404 ProblemDetails response
    - Test tenant isolation: verify 403 when JWT lacks requested tenantId claim
    - Test service throws `RpcException`: verify 503 ProblemDetails response with "Service Unavailable" title
    - Test CancellationToken: verify cancellation token propagated from action to service call
  - [ ] 12.3 Write `Controllers/AdminProjectionsControllerTests.cs`:
    - Test read operations delegate to `IProjectionQueryService`
    - Test write operations delegate to `IProjectionCommandService`
    - Test ReadOnly role rejected on write endpoints (403)
    - Test Operator role accepted on write endpoints
  - [ ] 12.4 Write `Controllers/AdminTenantsControllerTests.cs`:
    - Test Admin role accepted
    - Test Operator role rejected (403)
    - Test ReadOnly role rejected (403)
  - [ ] 12.5 Write `Controllers/AdminHealthControllerTests.cs`:
    - Test service delegation
    - Test any AdminRole accepted
  - [ ] 12.5b Write `Controllers/AdminStorageControllerTests.cs`:
    - Test read operations delegate to `IStorageQueryService`
    - Test write operations delegate to `IStorageCommandService`
    - Test ReadOnly role rejected on write endpoints (403)
    - Test Operator role accepted on write endpoints
  - [ ] 12.5c Write `Controllers/AdminDeadLettersControllerTests.cs`:
    - Test read operations delegate to `IDeadLetterQueryService`
    - Test write operations delegate to `IDeadLetterCommandService`
    - Test ReadOnly role rejected on write endpoints (403)
  - [ ] 12.5d Write `Controllers/AdminTypeCatalogControllerTests.cs`:
    - Test service delegation for events, commands, aggregates
    - Test domain filter parameter passed correctly
  - [ ] 12.6 Write `Authorization/AdminTenantAuthorizationFilterTests.cs`:
    - Test tenant claim present and matching — passes
    - Test tenant claim absent — 403
    - Test tenant claim present but not matching — 403
    - Test no tenantId route param — filter skips (passes)
  - [ ] 12.7 Write `Configuration/ServiceCollectionExtensionsTests.cs`:
    - Test `AddAdminApi()` registers all authorization policies
    - Test `AddAdminApi()` registers `AdminTenantAuthorizationFilter`
    - Test `AddAdminApi()` registers `AdminClaimsTransformation`
  - [ ] 12.8 Write authorization integration tests using `WebApplicationFactory` (CRITICAL — unit tests cannot verify `[Authorize]` attribute enforcement):
    - [ ] 12.8.1 Create `IntegrationTests/AdminTestHost.cs` — minimal `WebApplication` entry point for `WebApplicationFactory<AdminTestHost>`. Since Admin.Server is a class library (no Program.cs), the test project must create its own host:
      ```csharp
      // Minimal host that registers admin controllers with mock services
      var builder = WebApplication.CreateBuilder(args);
      builder.Services.AddAdminApi(builder.Configuration);
      builder.Services.AddAuthentication("Test")
          .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", null);
      builder.Services.AddControllers()
          .AddApplicationPart(typeof(AdminStreamsController).Assembly);

      // IMPORTANT: AddAdminApi() calls AddAdminServer() which registers
      // DAPR-backed service implementations. Override with NSubstitute mocks
      // so integration tests don't need a running DAPR sidecar:
      builder.Services.AddScoped(_ => Substitute.For<IStreamQueryService>());
      builder.Services.AddScoped(_ => Substitute.For<IProjectionQueryService>());
      builder.Services.AddScoped(_ => Substitute.For<IProjectionCommandService>());
      builder.Services.AddScoped(_ => Substitute.For<ITypeCatalogService>());
      builder.Services.AddScoped(_ => Substitute.For<IHealthQueryService>());
      builder.Services.AddScoped(_ => Substitute.For<IStorageQueryService>());
      builder.Services.AddScoped(_ => Substitute.For<IStorageCommandService>());
      builder.Services.AddScoped(_ => Substitute.For<IDeadLetterQueryService>());
      builder.Services.AddScoped(_ => Substitute.For<IDeadLetterCommandService>());
      builder.Services.AddScoped(_ => Substitute.For<ITenantQueryService>());

      var app = builder.Build();
      app.UseAuthentication();
      app.UseAuthorization();
      app.MapControllers();
      app.Run();
      ```
      NSubstitute mocks are registered AFTER `AddAdminApi()` so they override the real DAPR implementations. Integration tests focus on authorization pipeline enforcement, not service logic.
    - [ ] 12.8.2 Create `IntegrationTests/TestAuthHandler.cs` — `AuthenticationHandler<AuthenticationSchemeOptions>` that creates a `ClaimsPrincipal` from test-configured claims (via custom header or test fixture setup). Follow ASP.NET Core's standard test auth pattern.
    - [ ] 12.8.3 Create `IntegrationTests/AdminAuthorizationIntegrationTests.cs`:
      - Create a minimal `WebApplicationFactory` test fixture that registers admin controllers, policies, claims transformation, and the test auth handler (`AddAuthentication("Test").AddScheme<TestAuthHandler>(...)`)
      - Test: request with no auth → 401
      - Test: request with ReadOnly role to GET `/api/v1/admin/streams` → 200
      - Test: request with ReadOnly role to POST `/api/v1/admin/projections/{t}/{name}/pause` → 403
      - Test: request with Operator role to POST `/api/v1/admin/projections/{t}/{name}/pause` → 200 (or service-level result)
      - Test: request with Operator role to GET `/api/v1/admin/tenants` → 403
      - Test: request with Admin role to GET `/api/v1/admin/tenants` → 200
      - Test: request with valid role but wrong tenant claim → 403
  - [ ] 12.9 Write `Authorization/AdminClaimsTransformationTests.cs`:
    - Test: `global_admin` claim present → adds AdminRole = Admin
    - Test: `is_global_admin` claim present → adds AdminRole = Admin
    - Test: role claim `GlobalAdministrator` → adds AdminRole = Admin
    - Test: has `eventstore:permission` = `command:replay` (no global admin) → adds AdminRole = Operator
    - Test: has `eventstore:permission` = `command:submit` only (no replay, no global admin) → adds AdminRole = ReadOnly (NOT Operator — prevents privilege escalation)
    - Test: has `eventstore:tenant` only (no permissions, no global admin) → adds AdminRole = ReadOnly
    - Test: no relevant claims at all → no AdminRole claim added
    - Test: idempotency — AdminRole already present → no duplicate added
    - Test: exception-safety — malformed claims → returns identity unchanged, logs warning

- [ ] Task 13: Build and test (AC: all)
  - [ ] 13.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings
  - [ ] 13.2 `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/` — all pass
  - [ ] 13.3 Run all existing Tier 1 tests — 0 regressions

## Dev Notes

### Architecture: Admin.Server Is a Class Library, Not an Executable

Admin.Server remains a **class library** — not a web application host. The hosting (Program.cs, Kestrel, DAPR sidecar) is created in Story 14-4 (Aspire integration). This story adds:
1. `<FrameworkReference Include="Microsoft.AspNetCore.App" />` — enables controller base classes, JWT auth, and authorization in a class library
2. Controller classes that get discovered via `AddApplicationPart()` by the host
3. Authorization policies and filters
4. An `AddAdminApi()` extension method that the host's `Program.cs` will call

This follows ASP.NET Core's standard pattern for distributing controllers in class libraries.

### Controller Pattern — Follow CommandApi Conventions Exactly

Every existing controller in CommandApi uses this pattern. Admin controllers MUST follow the same conventions:

```csharp
[ApiController]
[Authorize]
[Route("api/v1/admin/{resource}")]
[Tags("Admin - {Resource}")]
public class Admin{Resource}Controller(
    I{Service} service,
    ILogger<Admin{Resource}Controller> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(TResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSomething(
        [FromQuery] string? tenantId,
        CancellationToken ct)
    {
        var result = await service.GetSomethingAsync(tenantId, ct).ConfigureAwait(false);
        return Ok(result);
    }
}
```

Key conventions:
- **Primary constructor** with injected services (C# 12 pattern used in CommandApi)
- **`ControllerBase`** base class (not `Controller` — no view support needed)
- **`[ApiController]`** attribute — enables automatic model validation, ProblemDetails error format
- **`[Authorize]`** at class or method level with policy names
- **`[ProducesResponseType]`** on every action for OpenAPI documentation (Story 14-5 builds on this)
- **`[Tags]`** for Swagger grouping
- **`ConfigureAwait(false)`** on all async calls
- **`CancellationToken ct`** parameter on every action, propagated to every service call — enables client disconnect cancellation
- **Route parameters** in URL path: `{tenantId}/{domain}/{aggregateId}`
- **Query parameters** via `[FromQuery]` for optional filters: `tenantId?`, `domain?`, `count?`
- **Request body** via `[FromBody]` for POST/PUT payloads

### Response Conventions

**Read endpoints:** Return `200 OK` with the raw DTO as JSON. No wrapper envelope — `[ApiController]` serializes the return value directly. Return `404 Not Found` with `ProblemDetails` when the service returns null.

**Write endpoints (synchronous):** Return `200 OK` with `AdminOperationResult` body. Map `AdminOperationResult.Success == false` to HTTP errors:
- `ErrorCode` = `"NotFound"` → 404
- `ErrorCode` = `"Unauthorized"` → 403
- `ErrorCode` = `"InvalidOperation"` → 422 Unprocessable Entity
- Other/null `ErrorCode` → 500

NOTE: Verify that Story 14-2 service implementations actually populate `AdminOperationResult.ErrorCode` with these values. If 14-2 uses different error codes, update this mapping to match. Check `DaprProjectionCommandService`, `DaprStorageCommandService`, and `DaprDeadLetterService` implementations.

**Write endpoints (async operations):** Operations that trigger background work (compaction, projection replay/reset) return `202 Accepted` with `AdminOperationResult` body containing an `OperationId` for status tracking.

### Controller Error Handling

Controllers MUST handle service-level exceptions directly — do NOT rely on a global exception handler that may not exist yet (host is Story 14-4):

```csharp
try
{
    var result = await service.GetSomethingAsync(tenantId, ct).ConfigureAwait(false);
    return result is null
        ? Problem(statusCode: 404, title: "Not Found")
        : Ok(result);
}
catch (Exception ex) when (ex is RpcException or HttpRequestException or TimeoutException)
{
    logger.LogError(ex, "Admin service unavailable: {Method}", nameof(GetSomething));
    return Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Service Unavailable",
        detail: "The admin backend service is temporarily unavailable. Retry shortly.");
}
```

This pattern:
- Catches DAPR/HTTP/timeout failures → 503 with actionable message
- Returns null results → 404
- Lets truly unexpected exceptions propagate (will get a generic 500)

### Route Design: `api/v1/admin/` Prefix

All admin endpoints use `api/v1/admin/` prefix to distinguish from CommandApi's `api/v1/commands/` and `api/v1/queries/`. Route structure:

| Controller | Route Base | Read Endpoints | Write Endpoints |
|-----------|-----------|----------------|-----------------|
| AdminStreamsController | `api/v1/admin/streams` | GET `/`, GET `/{t}/{d}/{a}/timeline`, GET `/{t}/{d}/{a}/state`, GET `/{t}/{d}/{a}/diff`, GET `/{t}/{d}/{a}/events/{seq}`, GET `/{t}/{d}/{a}/causation` | — |
| AdminProjectionsController | `api/v1/admin/projections` | GET `/`, GET `/{t}/{name}` | POST `/{t}/{name}/pause\|resume\|reset\|replay` |
| AdminTypeCatalogController | `api/v1/admin/types` | GET `/events`, GET `/commands`, GET `/aggregates` | — |
| AdminHealthController | `api/v1/admin/health` | GET `/`, GET `/dapr` | — |
| AdminStorageController | `api/v1/admin/storage` | GET `/overview`, GET `/hot-streams`, GET `/snapshot-policies` | POST `/{t}/compact`, POST `/{t}/{d}/{a}/snapshot`, PUT `/{t}/{d}/{at}/snapshot-policy` |
| AdminDeadLettersController | `api/v1/admin/dead-letters` | GET `/` | POST `/{t}/retry\|skip\|archive` |
| AdminTenantsController | `api/v1/admin/tenants` | GET `/`, GET `/{t}/quotas` | POST `/compare` |

### Authorization Model (NFR46) — Three-Tier Role Hierarchy

```
Admin > Operator > ReadOnly
```

Role mapping to JWT claims:
- JWT claim `eventstore:admin-role` with value `ReadOnly`, `Operator`, or `Admin`
- Policies are hierarchical: `Operator` policy accepts both `Operator` and `Admin` values; `Admin` policy accepts only `Admin`
- `ReadOnly` policy requires any admin role claim to be present (prevents unauthenticated access)

If `eventstore:admin-role` claim is absent, all admin endpoints return 403.

### Tenant Isolation (SEC-3) — Two-Layer Enforcement

**Layer 1: AdminTenantAuthorizationFilter (explicit tenant parameter)**

Follow the same tenant claim extraction pattern from `CommandStatusController`:
```csharp
var tenantClaims = User.FindAll("eventstore:tenant").Select(c => c.Value).ToList();
if (!tenantClaims.Contains(requestedTenantId, StringComparer.Ordinal))
{
    return Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Tenant Access Denied",
        detail: $"Not authorized for tenant '{requestedTenantId}'");
}
```

Implement as a reusable `AdminTenantAuthorizationFilter` (action filter). The filter:
1. Checks BOTH `RouteData.Values["tenantId"]` AND `HttpContext.Request.Query["tenantId"]` — query string parameters MUST be validated too, not just route parameters
2. If a tenantId is found in either location, validates against `eventstore:tenant` claims
3. If no tenantId in either location (e.g., health, type catalog), skips validation
4. Returns 403 `ProblemDetails` on failure

**Layer 2: Auto-scoping for optional-tenant list endpoints (CRITICAL)**

Endpoints with optional `tenantId?` (e.g., `GET /api/v1/admin/streams`, `GET /api/v1/admin/dead-letters`) MUST auto-scope to the caller's authorized tenants when no tenantId is specified. Otherwise a ReadOnly user with access to tenant-a could see streams from ALL tenants.

Implementation: When `tenantId` is null/absent, the controller extracts `User.FindAll("eventstore:tenant")` claims and passes them to the service as a filter. The service returns only data matching the caller's authorized tenants. Admin-role users (who have access to all tenants) can pass `tenantId = null` to see everything.

This is NOT handled by the filter (which only validates explicit tenant parameters) — it must be implemented in each controller action that accepts optional tenantId.

### Error Response Format — RFC 7807 ProblemDetails

All error responses MUST use `ProblemDetails` (D5):
- `status`: HTTP status code
- `title`: Short description (e.g., "Tenant Access Denied", "Not Found", "Service Unavailable")
- `detail`: Human-readable explanation (no stack traces per Enforcement #13)
- `instance`: Request path
- Extensions: `correlationId` (from `HttpContext` or generated)

See "Controller Error Handling" and "Response Conventions" sections for the complete error handling pattern.

### JWT Authentication — Admin.Server Is Auth-Scheme-Agnostic

Admin.Server does NOT own or configure JWT authentication. It only defines:
- Authorization policies (`AdminAuthorizationPolicies`)
- Claims transformation (`AdminClaimsTransformation`)
- Tenant isolation filter (`AdminTenantAuthorizationFilter`)

These work with **any** authentication scheme the host configures (JWT Bearer, cookies, test auth handler). The `<FrameworkReference Include="Microsoft.AspNetCore.App" />` provides all needed types (`[Authorize]`, `IClaimsTransformation`, `AuthorizationPolicy`, etc.).

Authentication registration (`AddAuthentication().AddJwtBearer()`, `ConfigureJwtBearerOptions`, `EventStoreAuthenticationOptions`) is 100% the **host's responsibility** (Story 14-4). Admin.Server has no dependency on these classes and does NOT reference CommandApi or ServiceDefaults for auth infrastructure.

### Admin Role Claim Mapping

The `eventstore:admin-role` claim is NOT present in standard Keycloak configurations. Admin.Server uses an `AdminClaimsTransformation : IClaimsTransformation` to map **existing claim patterns already used in CommandApi** to admin roles at request time:

- Has global admin role claims (`global_admin`, `is_global_admin`, `GlobalAdministrator` — same detection as `CommandsController`) → `Admin`
- Has `eventstore:permission` = `command:replay` specifically → `Operator` (only DBA/operators have replay; prevents `command:submit`-only users from controlling projections/compaction)
- Any authenticated user with valid `eventstore:tenant` claim → `ReadOnly`
- No relevant claims → no admin role (all admin endpoints return 403)

The transformation must be:
1. **Idempotent** — check for existing claim before adding (runs per-request)
2. **Exception-safe** — try/catch around all claim inspection; on failure, log warning and return identity unchanged
3. **OIDC-agnostic** — works with any provider, no IdP configuration changes needed

### Correlation ID

Extract from `HttpContext.Items["CorrelationId"]` if the host uses `CorrelationIdMiddleware` (from CommandApi). If not available, generate a new GUID. Include in all `ProblemDetails` responses and log entries.

### Performance Budget (NFR40)

- Read operations: < 500ms at p99
- Write operations: < 2s at p99
- Controllers add minimal overhead — they are thin delegation layers

### Dependencies After This Story

```
Admin.Server (class library):
  → Admin.Abstractions → Contracts
  → Dapr.Client
  → Microsoft.AspNetCore.App (FrameworkReference — includes auth, authorization, MVC)
  → Microsoft.Extensions.DependencyInjection.Abstractions
  → Microsoft.Extensions.Options
  → Microsoft.Extensions.Logging.Abstractions
  ✗ NO reference to CommandApi (sibling host)
  ✗ NO reference to ServiceDefaults (auth is host's concern)
  ✗ NO PackageReference for Microsoft.AspNetCore.Authentication.JwtBearer (FrameworkReference covers it)
```

### DO NOT

- Do NOT create a new executable/host project — the host is Story 14-4
- Do NOT create `Program.cs` — that's Story 14-4
- Do NOT add OpenAPI/Swagger setup — that's Story 14-5
- Do NOT implement business logic in controllers — delegate to service interfaces
- Do NOT create custom exception handler classes — rely on `[ApiController]`'s automatic `ProblemDetails` responses for model validation errors and use `return Problem(...)` in controller actions for known errors. The host (Story 14-4) configures the global exception handler.
- Do NOT bypass service interfaces — controllers call service methods, never DaprClient directly
- Do NOT use `[AllowAnonymous]` on any admin endpoint
- Do NOT log JWT tokens or event payload data (SEC-5, Enforcement #5)
- Do NOT return stack traces in error responses (Enforcement #13)
- Do NOT add rate limiting — rate limiting is configured at the host level (Story 14-4)
- Do NOT reference `Hexalith.EventStore.CommandApi` — Admin.Server and CommandApi are sibling hosts; Admin.Server is auth-scheme-agnostic and has no dependency on CommandApi's auth classes
- Do NOT call `services.AddControllers()` inside `AddAdminApi()` — the host project (Story 14-4) calls it once and adds `AddApplicationPart(typeof(AdminStreamsController).Assembly)` to discover admin controllers
- Do NOT call `services.AddAuthentication()` inside `AddAdminApi()` — the host owns auth registration to avoid double-registration when cohosted with CommandApi

### Naming and Organization Conventions

Follow existing patterns exactly:
- **Namespace**: `Hexalith.EventStore.Admin.Server.Controllers` for controllers, `Hexalith.EventStore.Admin.Server.Authorization` for auth infrastructure
- **File-scoped namespaces**: `namespace X.Y.Z;`
- **One public type per file**: File name = type name
- **Controller naming**: `Admin{Domain}Controller.cs` (e.g., `AdminStreamsController.cs`)
- **Braces**: Allman style (opening brace on new line)
- **Private fields**: `_camelCase`
- **Async methods**: `Async` suffix, all accept `CancellationToken`, all use `.ConfigureAwait(false)`
- **XML documentation**: On every public method
- **Indentation**: 4 spaces, CRLF line endings, UTF-8

### Test Conventions

- **Framework**: xUnit 2.9.3
- **Assertions**: Shouldly 4.3.0 (`result.ShouldNotBeNull()`, `statusCode.ShouldBe(200)`)
- **Mocking**: NSubstitute 5.3.0 — mock service interfaces, NOT DaprClient (service-level mocking)
- **Controller testing**: Instantiate controllers directly with mocked services (unit tests), OR use `WebApplicationFactory<T>` for integration tests if a minimal host is available
- **Pattern**: One test class per controller, test methods named `{Action}_{Scenario}_{ExpectedResult}`
- **Authorization testing (CRITICAL)**: `[Authorize(Policy = ...)]` attributes are only enforced by ASP.NET Core middleware, NOT by direct controller instantiation. Unit tests with mocked services verify delegation logic but NOT policy enforcement. At least one integration test per authorization tier (ReadOnly, Operator, Admin) using `WebApplicationFactory` with a test auth handler is REQUIRED to prevent authorization bypass vulnerabilities. Unit tests with `ClaimsPrincipal` are acceptable for `AdminTenantAuthorizationFilter` and `AdminClaimsTransformation` testing only.

### Previous Story Intelligence

**Story 14-1** created Admin.Abstractions with:
- 10 service interfaces (5 query, 3 command, 1 combined health, 1 type catalog)
- DTOs in `Models/{Feature}/` with `ToString()` redaction (SEC-5)
- `AdminRole` enum: ReadOnly, Operator, Admin
- `AdminOperationResult` for write operation responses
- `PagedResult<T>` for paginated queries
- 107 tests all passing

**Story 14-2** created Admin.Server with:
- DAPR-backed implementations of all 9 service interfaces
- `AdminServerOptions` configuration class
- `AddAdminServer()` DI registration
- `AdminStateStoreKeys` helper for key derivation
- NO ASP.NET Core dependencies (this story adds them)

### Project Structure After This Story

```
src/Hexalith.EventStore.Admin.Server/
  Hexalith.EventStore.Admin.Server.csproj  (modified: add ASP.NET Core refs)
  Authorization/
    AdminAuthorizationPolicies.cs    (NEW)
    AdminClaimTypes.cs               (NEW)
    AdminClaimsTransformation.cs     (NEW)
    AdminTenantAuthorizationFilter.cs (NEW)
  Configuration/
    AdminServerOptions.cs            (from 14-2)
    ServiceCollectionExtensions.cs   (modified: add AddAdminApi())
  Controllers/
    AdminStreamsController.cs         (NEW)
    AdminProjectionsController.cs    (NEW)
    AdminTypeCatalogController.cs    (NEW)
    AdminHealthController.cs         (NEW)
    AdminStorageController.cs        (NEW)
    AdminDeadLettersController.cs    (NEW)
    AdminTenantsController.cs        (NEW)
  Helpers/
    AdminStateStoreKeys.cs           (from 14-2)
    DaprServiceInvocationHelper.cs   (from 14-2)
  Models/
    DeadLetterActionRequest.cs       (NEW)
    TenantCompareRequest.cs          (NEW)
    ProjectionReplayRequest.cs       (NEW)
    ProjectionResetRequest.cs        (NEW)
  Services/
    Dapr*.cs                         (from 14-2, 9 files)

tests/Hexalith.EventStore.Admin.Server.Tests/
  Controllers/
    AdminStreamsControllerTests.cs       (NEW)
    AdminProjectionsControllerTests.cs  (NEW)
    AdminTypeCatalogControllerTests.cs  (NEW)
    AdminHealthControllerTests.cs       (NEW)
    AdminStorageControllerTests.cs      (NEW)
    AdminDeadLettersControllerTests.cs  (NEW)
    AdminTenantsControllerTests.cs      (NEW)
  Authorization/
    AdminTenantAuthorizationFilterTests.cs (NEW)
    AdminClaimsTransformationTests.cs      (NEW)
  IntegrationTests/
    TestAuthHandler.cs                     (NEW)
    AdminAuthorizationIntegrationTests.cs  (NEW)
  Configuration/
    ServiceCollectionExtensionsTests.cs (extend from 14-2 or NEW)
```

### References

- [Source: _bmad-output/planning-artifacts/architecture.md — ADR-P4: Admin Tooling Three-Interface Architecture]
- [Source: _bmad-output/planning-artifacts/architecture.md — D5: Error Response Format (RFC 7807 ProblemDetails)]
- [Source: _bmad-output/planning-artifacts/architecture.md — Six-Layer Security Defense]
- [Source: _bmad-output/planning-artifacts/architecture.md — Cross-Cutting #12: Admin Authentication]
- [Source: _bmad-output/planning-artifacts/architecture.md — Enforcement Guidelines #1, #7, #13]
- [Source: _bmad-output/planning-artifacts/prd.md — FR79 (three-interface Admin API), FR82 (observability deep links)]
- [Source: _bmad-output/planning-artifacts/prd.md — NFR40 (500ms/2s performance), NFR46 (role-based access control)]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-03-21-admin-tooling.md — Story 14-3 scope]
- [Source: _bmad-output/implementation-artifacts/14-1-admin-abstractions-service-interfaces-and-dtos.md — Interface definitions]
- [Source: _bmad-output/implementation-artifacts/14-2-admin-server-dapr-backed-service-implementations.md — Service implementations, DO NOT list]
- [Source: src/Hexalith.EventStore.CommandApi/Controllers/CommandsController.cs — Controller conventions]
- [Source: src/Hexalith.EventStore.CommandApi/Controllers/CommandStatusController.cs — Tenant claim extraction]
- [Source: src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs — DI registration patterns (read for conventions only, do NOT reference)]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
