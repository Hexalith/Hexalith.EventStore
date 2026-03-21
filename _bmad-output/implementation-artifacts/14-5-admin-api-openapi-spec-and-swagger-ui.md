# Story 14.5: Admin API OpenAPI Spec & Swagger UI

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer building admin tooling (CLI, MCP, or exploring the Admin API),
I want interactive API documentation with Swagger UI and an OpenAPI 3.1 specification for all admin endpoints,
So that I can explore, test, and understand the full Admin API without reading separate documentation.

## Acceptance Criteria

1. **Given** the Admin.Server host is running (from Story 14-4), **When** a consumer navigates to `/swagger`, **Then** Swagger UI loads showing all admin endpoints grouped by controller tags: "Admin - Streams", "Admin - Projections", "Admin - Type Catalog", "Admin - Health", "Admin - Storage", "Admin - Dead Letters", "Admin - Tenants".
2. **Given** the Admin.Server host is running, **When** a consumer requests `/openapi/v1.json`, **Then** a valid OpenAPI 3.1 specification is returned with document title "Hexalith EventStore Admin API", version "v1", and a description referencing the admin capabilities and error reference pages.
3. **Given** the OpenAPI document, **When** inspected, **Then** it defines a JWT Bearer security scheme identical to CommandApi's pattern (type: http, scheme: bearer, bearerFormat: JWT) and all operations require it via a global security requirement.
4. **Given** the OpenAPI document, **When** operations are inspected, **Then** every operation documents: success response types (200/202), 401 Unauthorized, 403 Forbidden, and 503 Service Unavailable responses — matching the `[ProducesResponseType]` attributes from Story 14-3 controllers.
5. **Given** the OpenAPI document, **When** admin-specific operations are inspected, **Then** role-based authorization requirements are documented in operation descriptions — indicating which admin role (ReadOnly, Operator, Admin) is required for each endpoint.
6. **Given** the Swagger UI "Try it out" feature, **When** used on a stream query endpoint, **Then** pre-populated example parameters are available for common operations (e.g., sample tenantId, domain, aggregateId values).
7. **Given** all admin error `type` URIs (e.g., `/api/v1/admin/problems/admin-unauthorized`, `/api/v1/admin/problems/admin-tenant-denied`), **When** opened in a browser on the Admin.Server host, **Then** they resolve to human-readable HTML documentation explaining the error, with an example and resolution guidance — following the CommandApi `ErrorReferenceEndpoints` pattern. Admin error routes use `/api/v1/admin/problems/` prefix (not shared `/problems/`) to avoid route collision when co-hosted with CommandApi.
8. **Given** `ServiceCollectionExtensions.AddAdminOpenApi()`, **When** called, **Then** it registers OpenAPI document generation with admin-specific transformers. The host (Story 14-4) calls `MapOpenApi()` and `UseSwaggerUI()` — this story provides only the library-side registration.
9. **Given** the OpenAPI setup, **When** the configuration key `EventStore:Admin:OpenApi:Enabled` is `false`, **Then** Swagger UI and the OpenAPI endpoint are not mapped. Default is `true` (same gating pattern as CommandApi).
10. **Given** a new or extended test class in `tests/Hexalith.EventStore.Admin.Server.Tests/`, **When** tests run, **Then** OpenAPI document generation is validated: document structure, tag grouping, security scheme, endpoint presence, and Swagger UI availability.

## Tasks / Subtasks

> **Priority tiers apply.** Tasks 1-3, 6-7, 8.1-8.3 are must-have. Tasks 3, 8.5-8.7 are should-have. Tasks 4, 5, 8.4 are nice-to-have. See "Task Priority Tiers" in Dev Notes. Stop at must-have if blocked.

### Core Tasks

- [ ] Task 0: Prerequisites (AC: all)
  - [ ] 0.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` — confirm baseline compiles (0 errors, 0 warnings)
  - [ ] 0.2 Verify Story 14-3 output: `src/Hexalith.EventStore.Admin.Server/Controllers/` contains all 7 admin controllers with `[Tags]`, `[ProducesResponseType]`, and `[ApiController]` attributes. If controllers exist but lack `[ProducesResponseType]` on some actions, add them as part of this story (not a blocker — the attributes are needed for OpenAPI response documentation). If controllers don't exist at all, STOP — this story depends on 14-3.
  - [ ] 0.2b Verify controller return types use `ActionResult<T>` (e.g., `Task<ActionResult<PagedResult<StreamSummary>>>`) — NOT `Task<IActionResult>`. If controllers return `IActionResult`, the OpenAPI spec shows response bodies as untyped `object`, which breaks client code generation for CLI (Epic 17) and MCP tool definitions (Epic 18). If return types are untyped, fix them as part of this story.
  - [ ] 0.3 Verify Story 14-4 output: An Admin.Server host project exists with `Program.cs`. If not, STOP — this story depends on 14-4 for the host.
  - [ ] 0.4 Read the existing CommandApi OpenAPI implementation as reference:
    - `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs` (lines 298-339) — `AddOpenApi()` with document transformer, security scheme, 429 response
    - `src/Hexalith.EventStore.CommandApi/Program.cs` (lines 33-39) — `MapOpenApi()` and `UseSwaggerUI()` gated by config
    - `src/Hexalith.EventStore.CommandApi/OpenApi/CommandExampleTransformer.cs` — `IOpenApiOperationTransformer` for pre-populated examples
    - `src/Hexalith.EventStore.CommandApi/OpenApi/ErrorReferenceEndpoints.cs` — error reference HTML pages
  - [ ] 0.5 Read the OpenAPI test pattern:
    - `tests/Hexalith.EventStore.Server.Tests/OpenApi/OpenApiSpecTests.cs` — document structure validation, tag grouping, security scheme
    - `tests/Hexalith.EventStore.Server.Tests/OpenApi/OpenApiWebApplicationFactory.cs` — test host setup with mocked dependencies
  - [ ] 0.6 Read `src/Hexalith.EventStore.Admin.Server/Configuration/ServiceCollectionExtensions.cs` — understand existing `AddAdminServer()` and `AddAdminApi()` (from 14-3)
  - [ ] 0.7 Read `Directory.Packages.props` — confirm package versions: `Microsoft.AspNetCore.OpenApi` (10.0.3), `Swashbuckle.AspNetCore.SwaggerUI` (10.1.2)

- [ ] Task 1: Add OpenAPI packages to Admin.Server (AC: #1, #2)
  - [ ] 1.1 Update `src/Hexalith.EventStore.Admin.Server/Hexalith.EventStore.Admin.Server.csproj` — add PackageReferences:
    ```xml
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
    <PackageReference Include="Swashbuckle.AspNetCore.SwaggerUI" />
    ```
    These use centralized versions from `Directory.Packages.props` (no version attribute in csproj).
    Admin.Server already has `<FrameworkReference Include="Microsoft.AspNetCore.App" />` from Story 14-3 — no change needed there.
  - [ ] 1.2 Verify `dotnet build Hexalith.EventStore.slnx --configuration Release` succeeds

- [ ] Task 2: Create admin OpenAPI document transformer (AC: #2, #3, #5)
  - [ ] 2.1 Create `OpenApi/AdminDocumentTransformer.cs`:
    ```csharp
    namespace Hexalith.EventStore.Admin.Server.OpenApi;

    /// <summary>
    /// Configures the OpenAPI document metadata and global security for the Admin API.
    /// Follows the same pattern as CommandApi's document transformer in
    /// ServiceCollectionExtensions.cs (lines 300-325).
    /// </summary>
    ```
    Implement as a lambda or class passed to `options.AddDocumentTransformer()`:
    - Set `document.Info` to:
      - Title: `"Hexalith EventStore Admin API"`
      - Version: `"v1"`
      - Description: `"Administration API for Hexalith EventStore — stream browsing, projection management, type catalog, health monitoring, storage operations, dead-letter management, and tenant administration. Requires JWT Bearer authentication with role-based access control (ReadOnly, Operator, Admin). Error reference documentation is available at /api/v1/admin/problems/{error-type} on this server."`
    - Add JWT Bearer security scheme (EXACT same structure as CommandApi):
      ```csharp
      OpenApiComponents components = document.Components ??= new OpenApiComponents();
      components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
      components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme {
          Type = SecuritySchemeType.Http,
          Scheme = "bearer",
          BearerFormat = "JWT",
          Description = "JWT Bearer token with admin role claims. Roles: ReadOnly (stream browsing, health), Operator (projection controls, snapshots), Admin (tenant management). Obtain from your identity provider.",
      };

      var schemeReference = new OpenApiSecuritySchemeReference("Bearer", document);
      document.Security ??= [];
      document.Security.Add(new OpenApiSecurityRequirement
      {
          { schemeReference, new List<string>() },
      });
      ```

- [ ] Task 3: Create admin operation transformer (AC: #4, #5)
  - [ ] 3.1 Create `OpenApi/AdminOperationTransformer.cs` implementing `IOpenApiOperationTransformer`:
    ```csharp
    namespace Hexalith.EventStore.Admin.Server.OpenApi;

    /// <summary>
    /// Adds common response documentation to admin operations only.
    /// Scoped to api/v1/admin/ prefix to avoid affecting CommandApi endpoints in co-hosted scenarios.
    /// </summary>
    public sealed class AdminOperationTransformer : IOpenApiOperationTransformer
    ```
    **CRITICAL: Path-prefix guard.** Every transformer MUST check the operation path before modifying:
    ```csharp
    // Guard: only transform admin endpoints (safe for co-hosted scenarios)
    string? path = context.Description.RelativePath;
    if (path is null || !path.StartsWith("api/v1/admin/", StringComparison.OrdinalIgnoreCase))
    {
        return Task.CompletedTask;
    }
    ```
    Without this guard, co-hosting Admin.Server with CommandApi causes admin transformers to modify CommandApi endpoints (both call `AddOpenApi()` which merges all transformers into a single document pipeline).

    Adds these responses as a safety net (if not already present via `[ProducesResponseType]`):
    - `401`: "Unauthorized — No valid JWT Bearer token provided"
    - `403`: "Forbidden — Insufficient admin role or tenant access denied"
    - `503`: "Service Unavailable — Admin backend service temporarily unavailable (DAPR/infrastructure)"
    Use `TryAdd()` to avoid overwriting responses already generated from controller attributes.

  - [ ] 3.2 Create `OpenApi/AdminRoleDescriptionTransformer.cs` implementing `IOpenApiOperationTransformer`:
    **CRITICAL: Include the same `api/v1/admin/` path-prefix guard as `AdminOperationTransformer` (Task 3.1).** Without this, co-hosted CommandApi endpoints get admin role descriptions injected.

    Inspects the endpoint metadata for `[Authorize(Policy = ...)]` attributes and appends role requirement text to the operation description:
    - `AdminReadOnly` policy → prepend "**Required role:** ReadOnly (or higher)"
    - `AdminOperator` policy → prepend "**Required role:** Operator (or Admin)"
    - `AdminFull` policy → prepend "**Required role:** Admin only"

    Implementation approach:
    ```csharp
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        // Extract [Authorize(Policy = ...)] from endpoint metadata
        var authorizeData = context.Description.ActionDescriptor
            .EndpointMetadata
            .OfType<AuthorizeAttribute>()
            .ToList();

        // CRITICAL: Use LastOrDefault — method-level [Authorize(Policy)] overrides
        // class-level. ASP.NET Core lists class attributes before method attributes
        // in EndpointMetadata. FirstOrDefault would pick the class-level policy (wrong).
        string? policy = authorizeData
            .Select(a => a.Policy)
            .LastOrDefault(p => p is not null);

        string roleText = policy switch
        {
            AdminAuthorizationPolicies.ReadOnly => "**Required role:** ReadOnly (or higher)\n\n",
            AdminAuthorizationPolicies.Operator => "**Required role:** Operator (or Admin)\n\n",
            AdminAuthorizationPolicies.Admin => "**Required role:** Admin only\n\n",
            _ => "**Required role:** Any authenticated admin user\n\n",
        };

        operation.Description = roleText + (operation.Description ?? string.Empty);
        return Task.CompletedTask;
    }
    ```
    NOTE: This uses `AdminAuthorizationPolicies` constants from Story 14-3's `Authorization/AdminAuthorizationPolicies.cs`. Verify the exact constant names before implementing.

    CAVEAT: The `context.Description.ActionDescriptor.EndpointMetadata` access path depends on .NET 10's OpenAPI pipeline exposing MVC metadata. If `EndpointMetadata` does not contain `AuthorizeAttribute` at OpenAPI generation time, use this fallback approach instead:
    ```csharp
    // Fallback: inspect FilterDescriptors for AuthorizeFilter
    var filters = context.Description.ActionDescriptor.FilterDescriptors;
    var authorizeFilter = filters
        .Select(f => f.Filter)
        .OfType<AuthorizeFilter>()
        .FirstOrDefault();
    string? policy = authorizeFilter?.Policy;
    ```
    If neither approach works, hardcode the role mapping by matching on `context.Description.RelativePath` patterns (admin/tenants → Admin, POST operations → Operator, GET operations → ReadOnly). Verify the chosen approach works in Task 8.5 unit tests BEFORE writing all other tests.

### Optional Enhancements (nice-to-have — skip if blocked or time-constrained)

- [ ] Task 4: Create admin example transformer (AC: #6)
  - [ ] 4.1 Create `OpenApi/AdminExampleTransformer.cs` implementing `IOpenApiOperationTransformer`:
    Follow the `CommandExampleTransformer` pattern. Add example parameters/bodies for key admin operations:
    - **GET /api/v1/admin/streams**: Example query parameters: `tenantId=tenant-a`, `domain=counter`, `count=50`
    - **GET /api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/timeline**: Example path: `tenantId=tenant-a`, `domain=counter`, `aggregateId=01JAXYZ1234567890ABCDEFJKM`
    - **POST /api/v1/admin/projections/{tenantId}/{projectionName}/pause**: Example path: `tenantId=tenant-a`, `projectionName=order-list`
    - **POST /api/v1/admin/dead-letters/{tenantId}/retry**: Example body:
      ```json
      { "messageIds": ["msg-001", "msg-002"] }
      ```

    Match route paths by checking `context.Description.RelativePath` and `context.Description.HttpMethod` — same guard pattern as `CommandExampleTransformer`.

- [ ] Task 5: Create admin error reference endpoints (AC: #7)
  - [ ] 5.1 Create `OpenApi/AdminErrorReferenceEndpoints.cs` — follow `ErrorReferenceEndpoints` pattern exactly:
    ```csharp
    namespace Hexalith.EventStore.Admin.Server.OpenApi;

    /// <summary>
    /// Serves admin-specific error reference documentation pages at /api/v1/admin/problems/{errorType}.
    /// </summary>
    public static class AdminErrorReferenceEndpoints
    ```
    Define admin-specific error models:
    - `admin-unauthorized`: "Admin Unauthorized" (403) — "The authenticated user does not have the required admin role for this operation."
    - `admin-tenant-denied`: "Admin Tenant Access Denied" (403) — "The authenticated user's JWT does not include the requested tenant in eventstore:tenant claims."
    - `admin-role-insufficient`: "Insufficient Admin Role" (403) — "The operation requires a higher admin role (Operator or Admin) than the user's current role."
    - `admin-service-unavailable`: "Admin Service Unavailable" (503) — "The admin backend service (DAPR state store or CommandApi) is temporarily unavailable."
    - `admin-not-found`: "Admin Resource Not Found" (404) — "The requested admin resource (stream, projection, tenant) was not found."

    Add `MapAdminErrorReferences()` extension method on `IEndpointRouteBuilder` that maps `/api/v1/admin/problems/{errorType}`. This uses the admin route prefix convention instead of sharing `/problems/` with CommandApi — avoids route collision in co-hosted scenarios where ASP.NET Core's first-match routing would cause CommandApi's error handler to intercept admin slugs and return 404.

    Use the `ExcludeFromDescription()` call to keep error reference endpoints out of the OpenAPI document — same as CommandApi.

### Core Tasks (continued)

- [ ] Task 6: Add OpenAPI registration to ServiceCollectionExtensions (AC: #8)
  - [ ] 6.1 Add `AddAdminOpenApi()` extension method to `Configuration/ServiceCollectionExtensions.cs`:
    ```csharp
    /// <summary>
    /// Registers OpenAPI document generation for the Admin API.
    /// The host must call <c>MapOpenApi()</c> and <c>UseSwaggerUI()</c> to enable endpoints.
    /// </summary>
    public static IServiceCollection AddAdminOpenApi(
        this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            // Document metadata and JWT security scheme
            options.AddDocumentTransformer((document, context, ct) =>
            {
                // ... admin document transformer logic from Task 2
                return Task.CompletedTask;
            });

            // Common response codes on all operations
            options.AddOperationTransformer<AdminOperationTransformer>();

            // Role descriptions per endpoint
            options.AddOperationTransformer<AdminRoleDescriptionTransformer>();

            // Pre-populated examples for key operations
            options.AddOperationTransformer<AdminExampleTransformer>();
        });

        return services;
    }
    ```
  - [ ] 6.2 Update `AddAdminApi()` to call `AddAdminOpenApi()`:
    ```csharp
    public static IServiceCollection AddAdminApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ... existing registrations (auth policies, claims, tenant filter, services) ...

        // OpenAPI document generation (Story 14-5)
        services.AddAdminOpenApi();

        return services;
    }
    ```
    IMPORTANT: `AddAdminOpenApi()` is a separate public method so hosts that don't want OpenAPI (e.g., production with `OpenApi:Enabled=false`) can skip calling it. However, calling `AddAdminApi()` always registers it — the config gating is in the host middleware (`MapOpenApi`/`UseSwaggerUI`), not in the service registration.

- [ ] Task 7: Update host Program.cs for OpenAPI middleware (AC: #1, #9)
  - [ ] 7.1 Add OpenAPI middleware to the Admin.Server host project's `Program.cs` (created in Story 14-4). Follow the CommandApi pattern:
    ```csharp
    // OpenAPI/Swagger UI (gated by configuration)
    if (app.Configuration.GetValue("EventStore:Admin:OpenApi:Enabled", true))
    {
        _ = app.MapOpenApi();
        _ = app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Hexalith EventStore Admin API v1");
            options.RoutePrefix = "swagger";
        });
    }

    // Admin error reference pages (served at /api/v1/admin/problems/{errorType})
    app.MapAdminErrorReferences();
    ```

    Place AFTER `app.UseAuthorization()` and BEFORE `app.MapControllers()` — same position as CommandApi.

    NOTE: If Story 14-4 already has this middleware wired up (anticipating Story 14-5), just verify the configuration. If not, add it.

- [ ] Task 8: Create tests (AC: #10)
  - [ ] 8.1 Create `OpenApi/AdminOpenApiWebApplicationFactory.cs` in `tests/Hexalith.EventStore.Admin.Server.Tests/`:
    Follow the `OpenApiWebApplicationFactory` pattern from `tests/Hexalith.EventStore.Server.Tests/OpenApi/`. Create a minimal test host that:
    - Calls `AddAdminApi(configuration)` (which includes `AddAdminOpenApi()`)
    - Adds test authentication handler
    - Registers NSubstitute mocks for all 10 admin service interfaces (overriding DAPR implementations)
    - Maps controllers with `AddApplicationPart(typeof(AdminStreamsController).Assembly)`
    - Enables OpenAPI via config: `["EventStore:Admin:OpenApi:Enabled"] = "true"`
    - Calls `MapOpenApi()`, `UseSwaggerUI()`, `MapAdminErrorReferences()`

    Admin.Server is a class library — no `Program` class. The test factory must create its own entry point (same approach as Story 14-3's `AdminTestHost`). Reuse `AdminTestHost` from 14-3 if available, extending it with OpenAPI middleware.

    CRITICAL: The test host middleware order MUST mirror Story 14-4's real host `Program.cs` exactly. Read 14-4's `Program.cs` and replicate the same `UseAuthentication()` → `UseAuthorization()` → `MapOpenApi()`/`UseSwaggerUI()` → `MapControllers()` order. Divergence between test host and real host is a common source of false-positive test passes.

  - [ ] 8.2 Create `OpenApi/AdminOpenApiDocumentTests.cs`:
    ```
    [Trait("Category", "Integration")]
    [Trait("Tier", "1")]
    public class AdminOpenApiDocumentTests : IClassFixture<AdminOpenApiWebApplicationFactory>
    ```
    Tests:
    - `OpenApiDocument_ReturnsValidJson` — GET `/openapi/v1.json` returns 200 with valid JSON
    - `OpenApiDocument_HasCorrectTitle` — title is "Hexalith EventStore Admin API"
    - `OpenApiDocument_HasCorrectVersion` — version is "v1"
    - `OpenApiDocument_ContainsBearerSecurityScheme` — security schemes contain "Bearer" with type "http", scheme "bearer"
    - `OpenApiDocument_ContainsExpectedTags` — tags array includes: "Admin - Streams", "Admin - Projections", "Admin - Type Catalog", "Admin - Health", "Admin - Storage", "Admin - Dead Letters", "Admin - Tenants"
    - `OpenApiDocument_StreamEndpoints_GroupedUnderStreamsTag` — verify `/api/v1/admin/streams` operations have tag "Admin - Streams"
    - `OpenApiDocument_ProjectionEndpoints_GroupedUnderProjectionsTag` — verify projection paths have correct tag
    - `OpenApiDocument_AllOperations_Have401And403Responses` — every operation includes 401 and 403 response documentation
    - `OpenApiDocument_StreamEndpoints_Have200And404Responses` — verify stream GET operations document 200 (success) and 404 (not found) response types from `[ProducesResponseType]` attributes
    - `OpenApiDocument_WriteEndpoints_Have503Response` — verify POST/PUT operations document 503 (service unavailable) response
    - `OpenApiDocument_OperatorEndpoints_DescribeRequiredRole` — POST projection endpoints include "Operator" role text in description
    - `OpenApiDocument_StreamEndpoints_HaveTypedResponseSchema` — verify GET `/api/v1/admin/streams` 200 response references a named schema (e.g., `PagedResultOfStreamSummary`) — NOT an untyped `object`. This validates that controllers use `ActionResult<T>` return types, which is critical for CLI code generation (Epic 17) and MCP tool definitions (Epic 18).
    - `OpenApiDocument_OperationIds_AreReadable` — verify operation IDs on admin endpoints are human-readable method names (e.g., `getRecentlyActiveStreams`) — not auto-generated path hashes. CLI and MCP consumers reference these by name.
    - `OpenApiDocument_HasMinimumExpectedPaths` — verify the document contains at least 15 path entries (7 controllers × ~2-4 endpoints). If count is zero, the host likely forgot `AddApplicationPart(typeof(AdminStreamsController).Assembly)`.
    - `OpenApiDocument_AtLeastOneOperation_HasXmlDocDescription` — verify at least one admin operation has a non-empty `description` field (populated from `/// <summary>` XML docs on controller methods). Prevents silent loss of documentation if `<GenerateDocumentationFile>` is removed.

  - [ ] 8.3 Create `OpenApi/AdminSwaggerUiTests.cs`:
    ```
    [Trait("Category", "Integration")]
    [Trait("Tier", "1")]
    ```
    Tests:
    - `SwaggerUi_ReturnsHtml` — GET `/swagger/index.html` returns 200 with HTML containing "swagger"
    - `SwaggerUi_InitializerJs_ReturnsJs` — GET `/swagger/swagger-initializer.js` returns JS containing "SwaggerUIBundle"

  - [ ] 8.4 _(nice-to-have — only if Task 5 was implemented)_ Create `OpenApi/AdminErrorReferenceTests.cs`:
    Tests:
    - `ErrorReference_KnownAdminError_ReturnsHtml` — GET `/api/v1/admin/problems/admin-unauthorized` returns 200 with HTML
    - `ErrorReference_UnknownError_Returns404` — GET `/api/v1/admin/problems/nonexistent-error` returns 404
    - `ErrorReference_AllAdminErrors_ReturnHtml` — iterate all admin error slugs, verify each returns 200

  - [ ] 8.5 Write `OpenApi/AdminRoleDescriptionTransformerTests.cs` (unit test):
    - Test: endpoint with ReadOnly policy → description contains "ReadOnly"
    - Test: endpoint with Operator policy → description contains "Operator"
    - Test: endpoint with Admin policy → description contains "Admin only"
    - Test: endpoint with no policy → description contains "Any authenticated"

  - [ ] 8.6 Write `OpenApi/AdminOpenApiConfigGatingTests.cs` (AC: #9):
    Create a second `WebApplicationFactory` variant (or parameterize the existing one) with `["EventStore:Admin:OpenApi:Enabled"] = "false"`:
    - `OpenApiEndpoint_WhenDisabled_Returns404` — GET `/openapi/v1.json` returns 404 (middleware not mapped)
    - `SwaggerUi_WhenDisabled_Returns404` — GET `/swagger/index.html` returns 404
    This prevents regression if someone refactors the host middleware gating logic.

  - [ ] 8.7 Write `OpenApi/AdminTransformerCoHostingSafetyTests.cs` (should-have tier):
    Validates that admin transformers do NOT modify non-admin endpoints — critical for co-hosted scenarios where CommandApi and Admin share the same OpenAPI document pipeline.
    - Create a test host that registers both admin controllers AND a mock non-admin controller (e.g., a minimal `[ApiController] [Route("api/v1/commands")] [Tags("Commands")]` test controller with one GET action)
    - `CoHosted_CommandApiEndpoint_HasNoAdminRoleDescription` — verify GET `/api/v1/commands` operation description does NOT contain "Required role" text
    - `CoHosted_CommandApiEndpoint_HasNoAdmin503Response` — verify the mock CommandApi endpoint does not have admin-injected 503 response (only responses from its own `[ProducesResponseType]`)
    - `CoHosted_AdminEndpoint_HasRoleDescription` — verify admin endpoints DO have role descriptions (positive control)
    This is the highest-risk failure mode in co-hosted deployments. The path-prefix guard (Task 3.1) prevents it, but without this test, a guard regression would silently corrupt the CommandApi spec.

- [ ] Task 9: Build and test (AC: all)
  - [ ] 9.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings
  - [ ] 9.2 `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/` — all pass (including new OpenAPI tests)
  - [ ] 9.3 Run all existing Tier 1 tests — 0 regressions
  - [ ] 9.4 Verify Swagger UI loads correctly by running the admin host and navigating to `/swagger`

## Dev Notes

### Dependencies — Must Be Complete Before Starting

- **Story 14-3** (Admin.Server REST API Controllers with JWT Auth): Provides all 7 controllers with `[ApiController]`, `[Authorize(Policy = ...)]`, `[Tags("Admin - ...")]`, and `[ProducesResponseType]` attributes. OpenAPI document generation relies on these attributes. **Required for all tasks.**
- **Story 14-4** (Admin.Server Aspire Resource Integration): Provides the Admin.Server host project with `Program.cs`. This story adds OpenAPI middleware calls to that host. **Required only for Task 7.** Tasks 1-6 and 8 (tests) can be implemented before 14-4 is complete — tests create their own `WebApplicationFactory` host and do not depend on 14-4's Program.cs.

### Controller Return Types — `ActionResult<T>` Required for Typed Schemas

Admin controllers MUST use typed return types (e.g., `Task<ActionResult<PagedResult<StreamSummary>>>`) — NOT `Task<IActionResult>`. If controllers return `IActionResult`, the OpenAPI spec generates untyped `object` response bodies, which:
- Breaks CLI code generation (Epic 17 — `eventstore-admin` uses the spec to auto-generate HTTP clients)
- Breaks MCP tool definitions (Epic 18 — tool response schemas derived from OpenAPI)
- Makes Swagger UI "Try it out" responses unreadable (no field names shown)

If Story 14-3 controllers use `IActionResult`, fix them in Task 0.2b as part of this story. The pattern to follow:
```csharp
[HttpGet]
[ProducesResponseType(typeof(PagedResult<StreamSummary>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public async Task<ActionResult<PagedResult<StreamSummary>>> GetRecentlyActiveStreams(...)
```

### Architecture: Follow CommandApi's OpenAPI Pattern Exactly

CommandApi's OpenAPI setup is the canonical reference. Admin API MUST follow the same patterns:

**Package references** (in `.csproj`, no version — uses centralized `Directory.Packages.props`):
- `Microsoft.AspNetCore.OpenApi` (v10.0.3) — .NET 10 built-in OpenAPI support via `AddOpenApi()`
- `Swashbuckle.AspNetCore.SwaggerUI` (v10.1.2) — Swagger UI static files only (not code generator)

**Service registration** (in `ServiceCollectionExtensions`):
```csharp
services.AddOpenApi(options => {
    options.AddDocumentTransformer(...);    // Document info, security
    options.AddOperationTransformer(...);   // Per-operation transformations
});
```

**Middleware** (in `Program.cs`):
```csharp
if (app.Configuration.GetValue("EventStore:Admin:OpenApi:Enabled", true))
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => {
        options.SwaggerEndpoint("/openapi/v1.json", "Hexalith EventStore Admin API v1");
        options.RoutePrefix = "swagger";
    });
}
```

**Key patterns from CommandApi** (`src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs:298-339`):
- Document transformer sets `document.Info` (title, version, description)
- Security scheme is `OpenApiSecurityScheme` with type `Http`, scheme `bearer`, format `JWT`
- Security requirement uses `OpenApiSecuritySchemeReference("Bearer", document)`
- Operation transformer adds standard response codes
- Custom `IOpenApiOperationTransformer` class for examples (guards on `RelativePath` and `HttpMethod`)

### OpenAPI Configuration Key

Use `EventStore:Admin:OpenApi:Enabled` (not `EventStore:OpenApi:Enabled` which is CommandApi's key). This allows independent control when admin and command APIs are co-hosted. Default is `true`.

### Co-Hosting Considerations

If Admin.Server is co-hosted with CommandApi in the same process:
- Both call `AddOpenApi()` which registers document generation
- .NET 10's `AddOpenApi()` creates a SINGLE document by default — both admin and command endpoints appear in the same `/openapi/v1.json`
- This is acceptable for development (shows full API surface)
- For production separation, named documents can be used: `AddOpenApi("admin-v1", ...)` producing `/openapi/admin-v1.json` — but this is a future optimization, NOT required for this story
- **All admin transformers include `api/v1/admin/` path-prefix guards** (Task 3.1, 3.2) so they skip CommandApi endpoints. Without these guards, admin role descriptions and response codes would be injected into CommandApi operations.
- Admin error reference endpoints use `/api/v1/admin/problems/{errorType}` (not shared `/problems/`) to avoid ASP.NET Core first-match routing conflicts where CommandApi's handler would intercept admin slugs and return 404.

### Swagger UI Security Note

Swagger UI at `/swagger` is served by Swashbuckle middleware — it is **NOT behind JWT authentication**. Anyone who can reach the admin server's port can view the full API surface, parameter names, and role hierarchy. This is the same exposure model as CommandApi's Swagger UI.
- **Development:** Acceptable — admin server runs on localhost
- **Production:** Disable via `EventStore:Admin:OpenApi:Enabled=false` OR place behind reverse proxy authentication. The config gating (AC #9) is the primary production control.

### Admin Error Reference Route

Admin error reference pages are served at `/api/v1/admin/problems/{errorType}` — NOT at the shared `/problems/` prefix used by CommandApi. This avoids route collision in co-hosted scenarios.

Admin error slugs:
`admin-unauthorized`, `admin-tenant-denied`, `admin-role-insufficient`, `admin-service-unavailable`, `admin-not-found`

### Task Priority Tiers

If the dev agent runs into time or complexity issues, tasks are prioritized:

**Must-have (core value):** Tasks 1, 2, 6, 7, 8.1-8.3 — OpenAPI document generation + Swagger UI + core tests. Without these, the story delivers nothing.

**Should-have (quality):** Tasks 3, 8.2 response type tests, 8.5, 8.6 — Operation transformers with path-prefix guards, role description transformer, config gating tests. These ensure correctness and co-hosting safety.

**Nice-to-have (polish):** Tasks 4, 5, 8.4 — Example transformer, error reference endpoints, error reference tests. These improve the developer experience but aren't blocking for CLI/MCP consumers.

### Duplicate Controller Registration Risk

The host from Story 14-4 MUST register admin controllers via `AddApplicationPart(typeof(AdminStreamsController).Assembly)` — NOT via assembly scanning (`AddControllers()` with default conventions). If both mechanisms are active, each admin endpoint appears twice in the OpenAPI document, producing duplicate paths and confusing Swagger UI. The `HasMinimumExpectedPaths` test (Task 8.2) would catch an abnormally high count, but a specific duplicate-detection assertion is not included — visual review of the Swagger UI in Task 9.4 is the secondary check.

### Downstream Story Guidance

This story's OpenAPI spec is consumed by:
- **Story 17-1** (CLI scaffold): Should use `/openapi/v1.json` to auto-generate the admin HTTP client. Test that the generated client compiles against the typed schemas.
- **Story 18-1** (MCP scaffold): Should derive MCP tool definitions from the OpenAPI operation IDs and response schemas.
- **Story 15-1** (Blazor Shell): Does NOT need the OpenAPI spec — it consumes admin services via DI, not HTTP. Do NOT add a second OpenAPI setup in the Blazor host.

### Admin Controller Tags (from Story 14-3)

The 7 controllers use these `[Tags]` values for OpenAPI grouping:
| Controller | Tag |
|-----------|-----|
| AdminStreamsController | "Admin - Streams" |
| AdminProjectionsController | "Admin - Projections" |
| AdminTypeCatalogController | "Admin - Type Catalog" |
| AdminHealthController | "Admin - Health" |
| AdminStorageController | "Admin - Storage" |
| AdminDeadLettersController | "Admin - Dead Letters" |
| AdminTenantsController | "Admin - Tenants" |

### Admin Route Structure (from Story 14-3)

All admin endpoints use `api/v1/admin/` prefix:
| Controller | Route Base | Read Endpoints | Write Endpoints |
|-----------|-----------|----------------|-----------------|
| AdminStreamsController | `api/v1/admin/streams` | GET `/`, GET `/{t}/{d}/{a}/timeline`, GET `/{t}/{d}/{a}/state`, GET `/{t}/{d}/{a}/diff`, GET `/{t}/{d}/{a}/events/{seq}`, GET `/{t}/{d}/{a}/causation` | -- |
| AdminProjectionsController | `api/v1/admin/projections` | GET `/`, GET `/{t}/{name}` | POST `/{t}/{name}/pause\|resume\|reset\|replay` |
| AdminTypeCatalogController | `api/v1/admin/types` | GET `/events`, GET `/commands`, GET `/aggregates` | -- |
| AdminHealthController | `api/v1/admin/health` | GET `/`, GET `/dapr` | -- |
| AdminStorageController | `api/v1/admin/storage` | GET `/overview`, GET `/hot-streams`, GET `/snapshot-policies` | POST `/{t}/compact`, POST `/{t}/{d}/{a}/snapshot`, PUT `/{t}/{d}/{at}/snapshot-policy` |
| AdminDeadLettersController | `api/v1/admin/dead-letters` | GET `/` | POST `/{t}/retry\|skip\|archive` |
| AdminTenantsController | `api/v1/admin/tenants` | GET `/`, GET `/{t}/quotas` | POST `/compare` |

### Authorization Model for OpenAPI Documentation

The three-tier role hierarchy from Story 14-3:
- **ReadOnly** — stream browsing, state inspection, type catalog, health, storage overview, dead-letter list
- **Operator** — ReadOnly + projection controls, snapshots, compaction, dead-letter retry/skip/archive
- **Admin** — Operator + tenant management, quotas, comparison

The `AdminRoleDescriptionTransformer` adds this role information to each operation's description in the OpenAPI doc, making it visible in Swagger UI.

### Testing Pattern — Follow OpenApiSpecTests

The test structure from `tests/Hexalith.EventStore.Server.Tests/OpenApi/OpenApiSpecTests.cs`:
1. `IClassFixture<AdminOpenApiWebApplicationFactory>` for shared test host
2. Helper `GetOpenApiDocumentAsync()` that fetches and parses `/openapi/v1.json`
3. Individual `[Fact]` methods validating specific document properties
4. JSON navigation using `JsonElement.GetProperty()` / `TryGetProperty()`
5. Shouldly assertions: `ShouldBe()`, `ShouldBeTrue()`, `ShouldContain()`

The test factory must mock all 10 admin service interfaces (NSubstitute) since OpenAPI generation only needs controller metadata, not live DAPR calls.

### File Structure After This Story

```
src/Hexalith.EventStore.Admin.Server/
  Hexalith.EventStore.Admin.Server.csproj  (modified: add OpenAPI packages)
  Configuration/
    ServiceCollectionExtensions.cs          (modified: add AddAdminOpenApi())
  OpenApi/                                  (NEW folder)
    AdminOperationTransformer.cs            (NEW)
    AdminRoleDescriptionTransformer.cs      (NEW)
    AdminExampleTransformer.cs              (NEW)
    AdminErrorReferenceEndpoints.cs         (NEW)

tests/Hexalith.EventStore.Admin.Server.Tests/
  OpenApi/                                  (NEW folder)
    AdminOpenApiWebApplicationFactory.cs    (NEW)
    AdminOpenApiDocumentTests.cs            (NEW)
    AdminSwaggerUiTests.cs                  (NEW)
    AdminErrorReferenceTests.cs             (NEW)
    AdminRoleDescriptionTransformerTests.cs (NEW)
```

Host project (from Story 14-4) — modified:
- `Program.cs` — add `MapOpenApi()`, `UseSwaggerUI()`, `MapAdminErrorReferences()` calls

### Naming and Organization Conventions

Follow existing patterns exactly:
- **Namespace**: `Hexalith.EventStore.Admin.Server.OpenApi` for all OpenAPI classes
- **File-scoped namespaces**: `namespace X.Y.Z;`
- **One public type per file**: File name = type name
- **Braces**: Allman style (opening brace on new line)
- **Async methods**: `Async` suffix, accept `CancellationToken`, use `.ConfigureAwait(false)`
- **XML documentation**: On every public method and class
- **Indentation**: 4 spaces, CRLF line endings, UTF-8

### Test Conventions

- **Framework**: xUnit 2.9.3
- **Assertions**: Shouldly 4.3.0 (`result.ShouldNotBeNull()`, `statusCode.ShouldBe(200)`)
- **Mocking**: NSubstitute 5.3.0 — mock service interfaces
- **Integration tests**: `WebApplicationFactory<T>` with custom entry point (Admin.Server is a class library)
- **Pattern**: Test methods named `{Subject}_{Scenario}_{ExpectedResult}`
- **Trait tags**: `[Trait("Category", "Integration")]`, `[Trait("Tier", "1")]` for integration tests

### DO NOT

- Do NOT use `Swashbuckle.AspNetCore` code generator — only `Swashbuckle.AspNetCore.SwaggerUI` for static files
- Do NOT use `NSwag` — the project uses .NET 10 built-in `Microsoft.AspNetCore.OpenApi`
- Do NOT add OpenAPI packages to Admin.Abstractions — only Admin.Server needs them
- Do NOT create named OpenAPI documents (`AddOpenApi("admin-v1", ...)`) — use default "v1" document name; co-hosting separation is a future optimization
- Do NOT implement OpenAPI generation manually — use `AddOpenApi()` with document/operation transformers
- Do NOT add business logic to transformers — they only manipulate OpenAPI metadata
- Do NOT reference CommandApi project — copy patterns, don't create dependencies
- Do NOT add `[AllowAnonymous]` to any endpoint (including Swagger UI — the static files are served by Swashbuckle middleware, not controllers)
- Do NOT add example transformers for EVERY endpoint — focus on the most common developer workflows (stream query, projection control, dead-letter retry)
- Do NOT log JWT tokens or event payload data in transformers or error reference pages (SEC-5)

### Previous Story Intelligence

**Story 14-1** (Admin Abstractions): Created 10 service interfaces, DTOs, `AdminRole` enum, `AdminOperationResult`, `PagedResult<T>`.

**Story 14-2** (Admin.Server DAPR Implementations): Created 10 DAPR-backed service implementations, `AdminServerOptions`, `AddAdminServer()` DI registration, `AdminStateStoreKeys`.

**Story 14-3** (Admin.Server REST Controllers): Created 7 controllers with `[Tags]`, `[ProducesResponseType]`, `[Authorize(Policy = ...)]` attributes, `AdminAuthorizationPolicies`, `AdminClaimsTransformation`, `AdminTenantAuthorizationFilter`, `AddAdminApi()` extension. Explicitly deferred OpenAPI to this story (14-5).

### Git Intelligence

Recent commits show:
- `3b530ff` — Admin.Server tests for DAPR services (Story 14-2)
- `c4deae4` — Admin.Abstractions model tests (Story 14-1)
- All admin work follows established patterns from CommandApi
- Test coverage is comprehensive with Shouldly/NSubstitute

### References

- [Source: src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs#AddOpenApi] — OpenAPI document transformer, security scheme, operation transformers
- [Source: src/Hexalith.EventStore.CommandApi/Program.cs#L33-39] — MapOpenApi/UseSwaggerUI gating pattern
- [Source: src/Hexalith.EventStore.CommandApi/OpenApi/CommandExampleTransformer.cs] — IOpenApiOperationTransformer pattern for examples
- [Source: src/Hexalith.EventStore.CommandApi/OpenApi/ErrorReferenceEndpoints.cs] — Error reference HTML pages pattern
- [Source: tests/Hexalith.EventStore.Server.Tests/OpenApi/OpenApiSpecTests.cs] — OpenAPI test patterns
- [Source: tests/Hexalith.EventStore.Server.Tests/OpenApi/OpenApiWebApplicationFactory.cs] — Test host factory pattern
- [Source: _bmad-output/implementation-artifacts/14-3-admin-server-rest-api-controllers-with-jwt-auth.md#DO NOT] — "Do NOT add OpenAPI/Swagger setup — that's Story 14-5"
- [Source: _bmad-output/planning-artifacts/architecture.md#ADR-P4] — Admin.Server hosts REST API + Web UI
- [Source: _bmad-output/planning-artifacts/prd.md#FR79] — All admin operations accessible through 3 interfaces via shared Admin API
- [Source: _bmad-output/planning-artifacts/prd.md#NFR40] — Admin API p99 < 500ms reads, < 2s writes
- [Source: _bmad-output/planning-artifacts/prd.md#NFR46] — Role-based access control

### Project Context Reference

- **.NET 10** (SDK 10.0.103) — uses built-in `Microsoft.AspNetCore.OpenApi`, NOT Swashbuckle code gen
- **Solution**: `Hexalith.EventStore.slnx` (modern XML format only)
- **Centralized packages**: `Directory.Packages.props` — all versions pinned there
- **Code style**: `.editorconfig` — file-scoped namespaces, Allman braces, 4-space indent, CRLF
- **Warnings as errors**: `TreatWarningsAsErrors = true`

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
