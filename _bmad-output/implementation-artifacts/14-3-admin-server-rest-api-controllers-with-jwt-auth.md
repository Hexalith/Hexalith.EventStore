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
4. **Given** a read-only endpoint (streams, projections list, type catalog, health, storage overview, dead-letter list, tenant list), **When** called with a valid JWT token bearing any `AdminRole` (ReadOnly, Operator, or Admin), **Then** the request is authorized.
5. **Given** an operator-level endpoint (projection pause/resume/reset/replay, compaction, snapshot creation, snapshot policy, dead-letter retry/skip/archive), **When** called with a JWT token bearing only `ReadOnly` role, **Then** a 403 Forbidden response is returned. **When** called with `Operator` or `Admin` role, **Then** the request is authorized.
6. **Given** an admin-level endpoint (tenant quota management, tenant comparison), **When** called with a JWT token bearing `ReadOnly` or `Operator` role, **Then** a 403 Forbidden response is returned. **When** called with `Admin` role, **Then** the request is authorized.
7. **Given** any endpoint receiving a tenant-scoped request, **When** the JWT token's `eventstore:tenant` claims do not include the requested tenant, **Then** a 403 Forbidden response is returned (SEC-3 tenant isolation).
8. **Given** any endpoint, **When** the underlying service throws or returns a failure, **Then** the controller returns appropriate HTTP status codes (404 for not found, 500 for infrastructure errors) with `ProblemDetails` format including `correlationId` extension — never stack traces (Enforcement #13).
9. **Given** `ServiceCollectionExtensions.AddAdminApi()`, **When** called, **Then** it registers JWT authentication (reusing `EventStoreAuthenticationOptions` from CommandApi config section), authorization policies for the three admin roles, and all admin controllers.
10. **Given** a new Tier 1 test project `tests/Hexalith.EventStore.Admin.Server.Tests/` (or extending the existing one from Story 14-2), **When** tests run, **Then** controller tests validate: correct service delegation, authorization policy enforcement, tenant isolation, error mapping to ProblemDetails, and correlation ID propagation.

## Tasks / Subtasks

- [ ] Task 0: Prerequisites (AC: all)
  - [ ] 0.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` — confirm baseline compiles (0 errors, 0 warnings)
  - [ ] 0.2 Verify Story 14-1 output: `src/Hexalith.EventStore.Admin.Abstractions/` exists with all 10 service interfaces. If not, STOP.
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
    - Add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to enable controller base classes and JWT auth in a class library
    - Add `<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />` (centralized version)
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
    - **GET** `/` → `ListDeadLetters(tenantId?, count?, continuationToken?)` — `[Authorize(Policy = ReadOnly)]`, delegates to `IDeadLetterQueryService.ListDeadLettersAsync`
    - **POST** `/{tenantId}/retry` → `RetryDeadLetters(tenantId, messageIds)` — `[Authorize(Policy = Operator)]`, request body: `DeadLetterActionRequest { IReadOnlyList<string> MessageIds }`
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
        // 1. JWT Authentication (reuse CommandApi's config section)
        services.AddOptions<EventStoreAuthenticationOptions>()
            .BindConfiguration("Authentication:JwtBearer")
            .ValidateOnStart();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // 2. Authorization policies (NFR46)
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AdminAuthorizationPolicies.ReadOnly, policy =>
                policy.RequireClaim(AdminClaimTypes.AdminRole));
            options.AddPolicy(AdminAuthorizationPolicies.Operator, policy =>
                policy.RequireClaim(AdminClaimTypes.AdminRole, "Operator", "Admin"));
            options.AddPolicy(AdminAuthorizationPolicies.Admin, policy =>
                policy.RequireClaim(AdminClaimTypes.AdminRole, "Admin"));
        });

        // 3. Tenant authorization filter
        services.AddScoped<AdminTenantAuthorizationFilter>();

        // 4. Register admin services (from Story 14-2)
        services.AddAdminServer(configuration);

        // 5. Controllers discovered via assembly scanning
        services.AddControllers()
            .AddApplicationPart(typeof(AdminStreamsController).Assembly);

        return services;
    }
    ```
    - IMPORTANT: `AddAdminApi()` calls `AddAdminServer()` internally — consumers only need to call `AddAdminApi()`
    - If `ConfigureJwtBearerOptions` class is internal to CommandApi, create an equivalent in Admin.Server or reference CommandApi. Check existing visibility first.

- [ ] Task 12: Create test project or extend existing (AC: #10)
  - [ ] 12.1 If `tests/Hexalith.EventStore.Admin.Server.Tests/` already exists from Story 14-2, add controller tests to it. Otherwise, create the project:
    - PackageReferences: xUnit, Shouldly, NSubstitute, coverlet.collector, `Microsoft.AspNetCore.Mvc.Testing` (for `WebApplicationFactory` integration tests)
    - ProjectReference: `../../src/Hexalith.EventStore.Admin.Server/Hexalith.EventStore.Admin.Server.csproj`
  - [ ] 12.2 Write `Controllers/AdminStreamsControllerTests.cs`:
    - Mock `IStreamQueryService` with NSubstitute
    - Test `GetRecentlyActiveStreams`: verify service called with correct parameters, verify 200 response
    - Test null result: verify 404 ProblemDetails response
    - Test tenant isolation: verify 403 when JWT lacks requested tenantId claim
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
  - [ ] 12.6 Write `Authorization/AdminTenantAuthorizationFilterTests.cs`:
    - Test tenant claim present and matching — passes
    - Test tenant claim absent — 403
    - Test tenant claim present but not matching — 403
    - Test no tenantId route param — filter skips (passes)
  - [ ] 12.7 Write `Configuration/ServiceCollectionExtensionsTests.cs`:
    - Test `AddAdminApi()` registers all authorization policies
    - Test `AddAdminApi()` registers `AdminTenantAuthorizationFilter`

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
- **Route parameters** in URL path: `{tenantId}/{domain}/{aggregateId}`
- **Query parameters** via `[FromQuery]` for optional filters: `tenantId?`, `domain?`, `count?`
- **Request body** via `[FromBody]` for POST/PUT payloads

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

### Tenant Isolation (SEC-3) — Reuse Existing Pattern

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

Implement this as a reusable `AdminTenantAuthorizationFilter` (action filter) rather than repeating in every action method. The filter:
1. Checks if the route contains a `tenantId` parameter
2. If present, validates against `eventstore:tenant` claims
3. If absent (e.g., health, type catalog), skips validation
4. Returns 403 `ProblemDetails` on failure

### Error Response Format — RFC 7807 ProblemDetails

All error responses MUST use `ProblemDetails` (D5):
- `status`: HTTP status code
- `title`: Short description (e.g., "Tenant Access Denied", "Not Found")
- `detail`: Human-readable explanation (no stack traces per Enforcement #13)
- `instance`: Request path
- Extensions: `correlationId` (from `HttpContext` or generated)

Follow the existing exception handler pattern from CommandApi. Admin controllers should:
- Return `Ok(result)` on success
- Return `Problem(...)` on known errors (null result → 404, tenant denied → 403)
- Let the global exception handler catch unexpected exceptions → 500

### JWT Authentication — Reuse CommandApi Configuration

Admin.Server reuses the **same JWT configuration** as CommandApi:
- Configuration section: `Authentication:JwtBearer`
- Options class: `EventStoreAuthenticationOptions` (from CommandApi)
- JWT validator: `ConfigureJwtBearerOptions` (from CommandApi)

IMPORTANT: Check whether `ConfigureJwtBearerOptions` and `EventStoreAuthenticationOptions` are `public` or `internal` in CommandApi. If `internal`:
- Option A: Make them `public` (preferred — Admin.Server references CommandApi)
- Option B: Duplicate in Admin.Server (not preferred — violates DRY)
- Option C: Extract to a shared auth package (over-engineering for now)

Admin.Server WILL reference CommandApi for auth infrastructure. This is acceptable because the host (Story 14-4) already references both CommandApi and Admin.Server.

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
  → Microsoft.AspNetCore.App (FrameworkReference)
  → Microsoft.AspNetCore.Authentication.JwtBearer
  → Microsoft.Extensions.DependencyInjection.Abstractions
  → Microsoft.Extensions.Options
  → Microsoft.Extensions.Logging.Abstractions
```

### DO NOT

- Do NOT create a new executable/host project — the host is Story 14-4
- Do NOT create `Program.cs` — that's Story 14-4
- Do NOT add OpenAPI/Swagger setup — that's Story 14-5
- Do NOT implement business logic in controllers — delegate to service interfaces
- Do NOT create new exception handler classes unless necessary — reuse or extend CommandApi's existing handlers
- Do NOT bypass service interfaces — controllers call service methods, never DaprClient directly
- Do NOT use `[AllowAnonymous]` on any admin endpoint
- Do NOT log JWT tokens or event payload data (SEC-5, Enforcement #5)
- Do NOT return stack traces in error responses (Enforcement #13)
- Do NOT add rate limiting — rate limiting is configured at the host level (Story 14-4)

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
- **Authorization testing**: Use `ClaimsPrincipal` with configured claims to test policy enforcement

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
- [Source: src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs — JWT auth, authorization DI]
- [Source: src/Hexalith.EventStore.CommandApi/Authentication/ — JWT configuration classes]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
