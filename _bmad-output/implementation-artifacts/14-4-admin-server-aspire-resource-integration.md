# Story 14.4: Admin.Server — Aspire Resource Integration

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer running the Hexalith EventStore locally,
I want the Admin.Server to be orchestrated as an Aspire resource with its own DAPR sidecar,
So that the admin API starts automatically alongside CommandApi in the local topology and can invoke CommandApi via DAPR service invocation.

## Acceptance Criteria

1. **Given** the solution, **When** built, **Then** a new project `src/Hexalith.EventStore.Admin.Server.Host/` exists as a web application (executable), compiles with zero errors/warnings, and is included in `Hexalith.EventStore.slnx`.
2. **Given** the Admin.Server.Host `Program.cs`, **When** inspected, **Then** it follows the identical bootstrap pattern as CommandApi: `AddServiceDefaults()`, `AddDaprClient()`, `AddAdminApi()`, `AddAuthentication().AddJwtBearer()`, `AddControllers().AddApplicationPart(typeof(AdminStreamsController).Assembly)`, middleware order: `CorrelationIdMiddleware` → `ExceptionHandler` → `Authentication` → `Authorization` → `MapControllers()`.
3. **Given** the AppHost `Program.cs`, **When** inspected, **Then** it registers `admin-server` as a project resource with a DAPR sidecar (AppId = `"admin-server"`), referencing the same state store and pub/sub components as CommandApi, plus a `WithReference(commandApi)` service discovery binding.
4. **Given** the Aspire hosting extension, **When** `AddHexalithEventStore()` is called, **Then** the returned `HexalithEventStoreResources` includes the `AdminServer` resource builder alongside CommandApi, StateStore, and PubSub.
5. **Given** the Admin.Server.Host running in Aspire, **When** called with valid JWT Bearer token, **Then** it serves all admin endpoints from Story 14-3 controllers under `api/v1/admin/` prefix.
6. **Given** the Admin.Server.Host, **When** health checks are queried at `/health`, `/alive`, and `/ready`, **Then** standard Aspire health check responses are returned (from `AddServiceDefaults()` + `MapDefaultEndpoints()`).
7. **Given** the DAPR access control configuration, **When** inspected, **Then** an `admin-server` policy entry exists allowing it to invoke `commandapi` via DAPR service invocation (matching the existing `commandapi` policy pattern).
8. **Given** Keycloak is enabled in AppHost, **When** Admin.Server.Host starts, **Then** it receives the same JWT auth environment variables (`Authentication__JwtBearer__Authority`, `Authentication__JwtBearer__Issuer`, `Authentication__JwtBearer__Audience`) as CommandApi, pointing to the Keycloak realm.
9. **Given** a Tier 1 test project `tests/Hexalith.EventStore.Admin.Server.Host.Tests/`, **When** tests run, **Then** host bootstrap, DI registration, and middleware configuration are validated.
10. **Given** the existing Tier 1 tests, **When** all are run, **Then** zero regressions — all previously passing tests still pass.

## Tasks / Subtasks

- [ ] Task 0: Prerequisites (AC: all)
  - [ ] 0.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` — confirm baseline compiles (0 errors, 0 warnings)
  - [ ] 0.2 Verify Story 14-3 output: `src/Hexalith.EventStore.Admin.Server/Controllers/` contains all 7 admin controllers with `[Tags]`, `[ProducesResponseType]`, and `[ApiController]` attributes. If not, STOP.
  - [ ] 0.3 Verify Story 14-3 output: `src/Hexalith.EventStore.Admin.Server/Configuration/ServiceCollectionExtensions.cs` contains `AddAdminApi()`. If not, STOP.
  - [ ] 0.4 Read existing host patterns for conventions:
    - `src/Hexalith.EventStore.CommandApi/Program.cs` — bootstrap order, middleware chain, OpenAPI gating, partial Program class for testing
    - `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs` — JWT auth registration pattern (`AddCommandApi()` registers auth, authorization, exception handlers)
    - `src/Hexalith.EventStore.CommandApi/Middleware/CorrelationIdMiddleware.cs` — correlation ID injection pattern
    - `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs` — `AddServiceDefaults()`, `MapDefaultEndpoints()`
  - [ ] 0.5 Read Aspire orchestration patterns:
    - `src/Hexalith.EventStore.AppHost/Program.cs` — how CommandApi is registered, Keycloak wiring, publisher environments
    - `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` — `AddHexalithEventStore()` extension
    - `src/Hexalith.EventStore.Aspire/HexalithEventStoreResources.cs` — resource record
  - [ ] 0.6 Read DAPR access control:
    - `src/Hexalith.EventStore.AppHost/DaprComponents/accesscontrol.yaml` — current policy entries

- [ ] Task 1: Create Admin.Server.Host web application project (AC: #1)
  - [ ] 1.1 Create `src/Hexalith.EventStore.Admin.Server.Host/Hexalith.EventStore.Admin.Server.Host.csproj`:
    ```xml
    <Project Sdk="Microsoft.NET.Sdk.Web">

      <PropertyGroup>
        <IsPackable>false</IsPackable>
      </PropertyGroup>

      <ItemGroup>
        <ProjectReference Include="../Hexalith.EventStore.Admin.Server/Hexalith.EventStore.Admin.Server.csproj" />
        <ProjectReference Include="../Hexalith.EventStore.ServiceDefaults/Hexalith.EventStore.ServiceDefaults.csproj" />
      </ItemGroup>

      <ItemGroup>
        <PackageReference Include="Dapr.AspNetCore" />
      </ItemGroup>

    </Project>
    ```
    Key decisions:
    - `Microsoft.NET.Sdk.Web` — produces an executable web application (unlike Admin.Server which is `Microsoft.NET.Sdk` class library)
    - References Admin.Server (controllers, authorization, services) and ServiceDefaults (Aspire integration, health checks, OpenTelemetry)
    - `Dapr.AspNetCore` — for `AddDaprClient()` and DAPR integration in ASP.NET Core host
    - `IsPackable=false` — this is a deployment artifact, NOT a NuGet package
    - NO explicit `<FrameworkReference Include="Microsoft.AspNetCore.App" />` — `Microsoft.NET.Sdk.Web` includes it automatically
    - NO reference to CommandApi — Admin.Server.Host and CommandApi are sibling hosts; they communicate via DAPR service invocation only
  - [ ] 1.2 Add project to `Hexalith.EventStore.slnx`
  - [ ] 1.3 Verify `dotnet build Hexalith.EventStore.slnx --configuration Release` succeeds

- [ ] Task 2: Create Admin.Server.Host Program.cs (AC: #2, #5, #6)
  - [ ] 2.1 Create `src/Hexalith.EventStore.Admin.Server.Host/Program.cs`:
    ```csharp
    using Hexalith.EventStore.Admin.Server.Configuration;
    using Hexalith.EventStore.Admin.Server.Controllers;
    using Hexalith.EventStore.ServiceDefaults;

    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.AddServiceDefaults();
    builder.Services.AddDaprClient();
    builder.Services.AddAdminApi(builder.Configuration);

    // Authentication: same JWT Bearer pattern as CommandApi.
    // Authority is configured via environment variables from Aspire/Keycloak.
    builder.Services.AddAuthentication("Bearer")
        .AddJwtBearer("Bearer", options =>
        {
            // Bind from configuration — Aspire sets these via environment variables
            builder.Configuration.GetSection("Authentication:JwtBearer").Bind(options);
        });

    builder.Services.AddControllers()
        .AddApplicationPart(typeof(AdminStreamsController).Assembly);

    WebApplication app = builder.Build();

    app.UseExceptionHandler();
    app.MapDefaultEndpoints();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();

    /// <summary>
    /// Entry point class, made partial for WebApplicationFactory test access.
    /// </summary>
    public partial class Program;
    ```
    Key conventions (matching CommandApi exactly):
    - `AddServiceDefaults()` FIRST — Aspire resilience, telemetry, health checks, service discovery
    - `AddDaprClient()` — DAPR sidecar communication
    - `AddAdminApi(configuration)` — admin authorization policies, claims transformation, tenant filter, all 10 DAPR-backed services
    - Auth registration: `AddAuthentication().AddJwtBearer()` with config binding — Aspire injects Authority/Issuer/Audience via environment variables
    - `AddControllers().AddApplicationPart()` — discovers Admin.Server controllers from the class library assembly
    - Middleware order: `UseExceptionHandler` → `MapDefaultEndpoints` → `UseAuthentication` → `UseAuthorization` → `MapControllers`
    - `public partial class Program;` — enables `WebApplicationFactory<Program>` in test projects
    - NO `CorrelationIdMiddleware` — Admin.Server does not have its own; if needed later, add in a follow-up. Controllers already handle correlation IDs via HttpContext.
    - NO `UseCloudEvents()` or `MapSubscribeHandler()` — Admin.Server does not subscribe to pub/sub
    - NO `MapActorsHandlers()` — Admin.Server does not host DAPR actors
    - NO OpenAPI/Swagger in this story — that's Story 14-5

  - [ ] 2.2 CRITICAL: Verify `AddAdminApi()` calls `AddAdminServer()` internally — consumers only call `AddAdminApi()` + host-level auth/controller registration. The existing code in `ServiceCollectionExtensions.cs` already does this (line 46: `services.AddAdminServer(configuration);`).

  - [ ] 2.3 Create `src/Hexalith.EventStore.Admin.Server.Host/Properties/launchSettings.json`:
    ```json
    {
      "$schema": "http://json.schemastore.org/launchsettings.json",
      "profiles": {
        "http": {
          "commandName": "Project",
          "dotnetRunMessages": true,
          "launchBrowser": false,
          "applicationUrl": "http://localhost:8090",
          "environmentVariables": {
            "ASPNETCORE_ENVIRONMENT": "Development"
          }
        },
        "https": {
          "commandName": "Project",
          "dotnetRunMessages": true,
          "launchBrowser": false,
          "applicationUrl": "https://localhost:8091;http://localhost:8090",
          "environmentVariables": {
            "ASPNETCORE_ENVIRONMENT": "Development"
          }
        }
      }
    }
    ```
    Port 8090 avoids conflicts: CommandApi=8080, Keycloak=8180, Sample=8081.

  - [ ] 2.4 Create `src/Hexalith.EventStore.Admin.Server.Host/appsettings.json`:
    ```json
    {
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.AspNetCore": "Warning"
        }
      },
      "AllowedHosts": "*",
      "AdminServer": {
        "StateStoreName": "statestore",
        "CommandApiAppId": "commandapi",
        "TenantsServiceAppId": "tenants"
      }
    }
    ```

  - [ ] 2.5 Create `src/Hexalith.EventStore.Admin.Server.Host/appsettings.Development.json`:
    ```json
    {
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.AspNetCore": "Warning",
          "Hexalith": "Debug"
        }
      }
    }
    ```

- [ ] Task 3: Update Aspire hosting extension (AC: #3, #4)
  - [ ] 3.1 Update `src/Hexalith.EventStore.Aspire/HexalithEventStoreResources.cs` — add `AdminServer` property:
    ```csharp
    public record HexalithEventStoreResources(
        IResourceBuilder<IDaprComponentResource> StateStore,
        IResourceBuilder<IDaprComponentResource> PubSub,
        IResourceBuilder<ProjectResource> CommandApi,
        IResourceBuilder<ProjectResource> AdminServer);
    ```
    This is a **BREAKING CHANGE** to the record constructor — any existing consumer passing positional arguments must update. Since this is an internal hosting extension (not a published NuGet API), this is acceptable.

  - [ ] 3.2 Update `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` — add `adminServer` parameter and wire it with DAPR sidecar:
    ```csharp
    public static HexalithEventStoreResources AddHexalithEventStore(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> commandApi,
        IResourceBuilder<ProjectResource> adminServer,
        string? daprConfigPath = null)
    {
        // ... existing stateStore and pubSub setup ...

        // Wire CommandApi with DAPR sidecar (existing)
        _ = commandApi
            .WithDaprSidecar(sidecar => sidecar
                .WithOptions(new DaprSidecarOptions {
                    AppId = "commandapi",
                    Config = daprConfigPath,
                })
                .WithReference(stateStore)
                .WithReference(pubSub));

        // Wire Admin.Server with DAPR sidecar
        // Admin.Server needs state store for direct reads (health, admin indexes)
        // and service invocation to CommandApi for write delegation (ADR-P4).
        _ = adminServer
            .WithReference(commandApi)
            .WithDaprSidecar(sidecar => sidecar
                .WithOptions(new DaprSidecarOptions {
                    AppId = "admin-server",
                    Config = daprConfigPath,
                })
                .WithReference(stateStore)
                .WithReference(pubSub));

        return new HexalithEventStoreResources(stateStore, pubSub, commandApi, adminServer);
    }
    ```
    Key decisions:
    - `adminServer` is a **required parameter** (not optional) — Admin.Server is a core part of the EventStore topology
    - Admin.Server's DAPR sidecar gets the same `stateStore` and `pubSub` references as CommandApi — it needs state store for read operations (health checks, admin indexes)
    - `WithReference(commandApi)` — Aspire service discovery so Admin.Server can resolve CommandApi's endpoint
    - AppId = `"admin-server"` — matches the access control policy entry (Task 4)
    - Same `daprConfigPath` — loads the same access control configuration

  - [ ] 3.3 Update `src/Hexalith.EventStore.Aspire/Hexalith.EventStore.Aspire.csproj` — no changes expected (existing `Aspire.Hosting` and `CommunityToolkit.Aspire.Hosting.Dapr` dependencies suffice)

- [ ] Task 4: Update AppHost Program.cs (AC: #3, #8)
  - [ ] 4.1 Update `src/Hexalith.EventStore.AppHost/Program.cs` — add Admin.Server resource:
    ```csharp
    // Add Admin.Server host with DAPR sidecar
    IResourceBuilder<ProjectResource> adminServer = builder.AddProject<Projects.Hexalith_EventStore_Admin_Server_Host>("admin-server");
    HexalithEventStoreResources eventStoreResources = builder.AddHexalithEventStore(commandApi, adminServer, accessControlConfigPath);
    ```
    Also wire Keycloak auth to Admin.Server (same env vars as CommandApi):
    ```csharp
    if (keycloak is not null && realmUrl is not null)
    {
        _ = adminServer
            .WithReference(keycloak)
            .WaitFor(keycloak)
            .WithEnvironment("Authentication__JwtBearer__Authority", realmUrl)
            .WithEnvironment("Authentication__JwtBearer__Issuer", realmUrl)
            .WithEnvironment("Authentication__JwtBearer__Audience", "hexalith-eventstore")
            .WithEnvironment("Authentication__JwtBearer__RequireHttpsMetadata", "false")
            .WithEnvironment("Authentication__JwtBearer__SigningKey", "");
    }
    ```

  - [ ] 4.2 Update `src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` — add project reference:
    ```xml
    <ProjectReference Include="..\Hexalith.EventStore.Admin.Server.Host\Hexalith.EventStore.Admin.Server.Host.csproj" />
    ```

- [ ] Task 5: Update DAPR access control (AC: #7)
  - [ ] 5.1 Update `src/Hexalith.EventStore.AppHost/DaprComponents/accesscontrol.yaml` — add `admin-server` policy entry:
    ```yaml
    # admin-server: Admin API host -- invokes commandapi for write delegation (ADR-P4)
    # Admin reads go to DAPR state store directly; writes delegate to commandapi.
    - appId: admin-server
      defaultAction: deny
      trustDomain: "public"
      namespace: "default"
      operations:
        - name: /**
          httpVerb: ['GET', 'POST', 'PUT']
          action: allow
    ```
    Also update the header comment to reflect 3 services:
    ```yaml
    # App-ID Topology (3 services):
    #   - commandapi (port 8080): REST API host + DAPR actor host + event publisher
    #   - admin-server (port 8090): Admin REST API host -- reads state store, delegates writes to commandapi via DAPR service invocation
    #   - sample (port 8081): Reference domain service. Zero infrastructure access.
    ```

- [ ] Task 6: Create test project (AC: #9)
  - [ ] 6.1 Create `tests/Hexalith.EventStore.Admin.Server.Host.Tests/Hexalith.EventStore.Admin.Server.Host.Tests.csproj`:
    ```xml
    <Project Sdk="Microsoft.NET.Sdk">

      <PropertyGroup>
        <IsPackable>false</IsPackable>
      </PropertyGroup>

      <ItemGroup>
        <ProjectReference Include="../../src/Hexalith.EventStore.Admin.Server.Host/Hexalith.EventStore.Admin.Server.Host.csproj" />
      </ItemGroup>

      <ItemGroup>
        <PackageReference Include="coverlet.collector" />
        <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
        <PackageReference Include="Microsoft.NET.Test.Sdk" />
        <PackageReference Include="NSubstitute" />
        <PackageReference Include="Shouldly" />
        <PackageReference Include="xunit" />
        <PackageReference Include="xunit.runner.visualstudio" />
      </ItemGroup>

    </Project>
    ```
  - [ ] 6.2 Add project to `Hexalith.EventStore.slnx`
  - [ ] 6.3 Write `HostBootstrapTests.cs`:
    - Test: `WebApplicationFactory<Program>` builds successfully with mock DaprClient
    - Test: `AddAdminApi()` registers all authorization policies (AdminReadOnly, AdminOperator, AdminFull)
    - Test: `AddAdminApi()` registers `AdminClaimsTransformation`
    - Test: `AddAdminApi()` registers `AdminTenantAuthorizationFilter`
    - Test: `AddAdminApi()` registers all 10 admin service implementations
    - Test: `AddControllers().AddApplicationPart()` discovers admin controllers
    - Test: Health check endpoints respond (GET `/health` → 200)
  - [ ] 6.4 Write `MiddlewareOrderTests.cs`:
    - Test: unauthenticated request to `/api/v1/admin/streams` → 401 (auth middleware active)
    - Test: authenticated request with valid admin role → 200 (or service-level result)

- [ ] Task 7: Build and test (AC: #10)
  - [ ] 7.1 `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings
  - [ ] 7.2 `dotnet test tests/Hexalith.EventStore.Admin.Server.Host.Tests/` — all pass
  - [ ] 7.3 `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/` — all existing 154 tests pass (0 regressions)
  - [ ] 7.4 Run all Tier 1 tests — 0 regressions

## Dev Notes

### Architecture: Admin.Server.Host vs Admin.Server

Admin.Server is a **class library** — it contains controllers, authorization, and DAPR service implementations but has no `Program.cs` or executable entry point. This story creates `Admin.Server.Host`, the **executable web application** that hosts Admin.Server in an Aspire-managed process with its own DAPR sidecar.

This mirrors the existing pattern where `Hexalith.EventStore.Server` (class library) is hosted by `Hexalith.EventStore.CommandApi` (executable).

```
Admin.Server (class library)       CommandApi (executable)
├─ Controllers/                    ├─ Program.cs
├─ Authorization/                  ├─ Extensions/
├─ Services/                       ├─ Middleware/
├─ Configuration/                  └─ ...
└─ Models/
         ↓                                ↓
Admin.Server.Host (executable)     (self-contained)
├─ Program.cs
├─ appsettings.json
└─ Properties/launchSettings.json
```

### Bootstrap Pattern — Follow CommandApi Exactly

The Admin.Server.Host `Program.cs` follows the same bootstrap sequence as CommandApi's `Program.cs`:

```csharp
// 1. Aspire integration (telemetry, health, service discovery)
builder.AddServiceDefaults();

// 2. DAPR client for state store reads + service invocation
builder.Services.AddDaprClient();

// 3. Admin API registration (auth policies, services)
builder.Services.AddAdminApi(builder.Configuration);

// 4. JWT authentication (host responsibility — not in AddAdminApi)
builder.Services.AddAuthentication("Bearer").AddJwtBearer("Bearer", ...);

// 5. Controller discovery from class library
builder.Services.AddControllers().AddApplicationPart(...);

// 6. Middleware order
app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

### DAPR Sidecar Configuration (ADR-P4)

Admin.Server gets its own DAPR sidecar with AppId `"admin-server"`:
- **State store access**: Direct reads for health checks and admin-specific indexes
- **Service invocation**: Delegates all writes to CommandApi via `DaprClient.InvokeMethodAsync()` — the DAPR service invocation building block handles mTLS, retries, and service discovery
- **Pub/Sub**: Referenced for completeness but Admin.Server does NOT publish or subscribe — it delegates write operations to CommandApi which handles publication
- **Access control**: `admin-server` has a policy entry allowing it to invoke `commandapi` via GET/POST/PUT

### Aspire Resource Topology After This Story

```
AppHost
├─ Redis (state store backing)
├─ DAPR state store (in-memory, actor-enabled)
├─ DAPR pub/sub
├─ CommandApi (AppId: "commandapi", port 8080)
│  └─ DAPR sidecar → state store + pub/sub
├─ Admin.Server.Host (AppId: "admin-server", port 8090)  ← NEW
│  └─ DAPR sidecar → state store + pub/sub + service invocation to commandapi
├─ Sample domain service (AppId: "sample", port 8081)
│  └─ DAPR sidecar (no infra access)
├─ Blazor UI (service discovery → commandapi)
└─ Keycloak (port 8180, optional)
```

### JWT Authentication — Identical to CommandApi

Admin.Server.Host receives the same Keycloak environment variables as CommandApi:
- `Authentication__JwtBearer__Authority` → Keycloak realm URL
- `Authentication__JwtBearer__Issuer` → Keycloak realm URL
- `Authentication__JwtBearer__Audience` → `hexalith-eventstore`
- `Authentication__JwtBearer__RequireHttpsMetadata` → `false` (local dev)
- `Authentication__JwtBearer__SigningKey` → cleared (force OIDC discovery mode)

When Keycloak is disabled, the host falls back to symmetric key auth via `Authentication:JwtBearer:SigningKey` in appsettings/secrets.

IMPORTANT: The JWT auth configuration in `Program.cs` must bind from the `Authentication:JwtBearer` configuration section — NOT hardcode values. Aspire sets these via environment variables at runtime. Use `builder.Configuration.GetSection("Authentication:JwtBearer").Bind(options)` or the equivalent configuration-based setup that CommandApi uses. Read `src/Hexalith.EventStore.CommandApi/Extensions/ServiceCollectionExtensions.cs` to see the exact auth registration pattern and replicate it.

### Aspire Extension Breaking Change

Adding `adminServer` to `HexalithEventStoreResources` and `AddHexalithEventStore()` is a **breaking change** to the public API surface. Since:
- `Hexalith.EventStore.Aspire` is consumed only by the AppHost (internal)
- The AppHost call site is updated in the same story (Task 4)
- No external consumers exist yet

This is safe. If external consumers ever exist, add an overload with backward compatibility.

### DAPR Access Control Update

The access control YAML adds `admin-server` as a permitted caller:
- `commandapi` policy already allows POST on `/**` — no changes needed for incoming calls TO commandapi FROM admin-server
- New `admin-server` policy: `defaultAction: deny` with operations allowing GET/POST/PUT on `/**` — controls what can call INTO admin-server

In production with mTLS, the `admin-server` sidecar's SPIFFE identity would be verified before allowing invocations to `commandapi`.

### DO NOT

- Do NOT put business logic in Program.cs — delegate to `AddAdminApi()` and Admin.Server class library
- Do NOT reference CommandApi project — sibling hosts communicate via DAPR only
- Do NOT create a new NuGet package for Admin.Server.Host — it's `IsPackable=false`
- Do NOT add OpenAPI/Swagger setup — that's Story 14-5
- Do NOT add rate limiting — rate limiting configuration is a future concern
- Do NOT add custom middleware (CorrelationId, etc.) beyond what's shown — keep it minimal
- Do NOT modify Admin.Server class library code — this story only creates the host and Aspire wiring
- Do NOT change existing CommandApi or Sample wiring in AppHost — only ADD Admin.Server alongside them
- Do NOT hardcode JWT auth configuration — use configuration binding from environment variables
- Do NOT add `Dapr.Actors.AspNetCore` — Admin.Server does not host actors

### Naming and Organization Conventions

Follow existing patterns exactly:
- **Project name**: `Hexalith.EventStore.Admin.Server.Host` (mirrors how CommandApi hosts Server)
- **Namespace**: `Hexalith.EventStore.Admin.Server.Host` for the Program class
- **File-scoped namespaces**: `namespace X.Y.Z;`
- **Braces**: Allman style
- **Indentation**: 4 spaces, CRLF line endings, UTF-8
- **Test project**: `Hexalith.EventStore.Admin.Server.Host.Tests`
- **Test class naming**: `{ClassUnderTest}Tests`
- **Test method naming**: `{Method}_{Scenario}_{ExpectedResult}`

### Test Conventions

- **Framework**: xUnit 2.9.3
- **Assertions**: Shouldly 4.3.0
- **Mocking**: NSubstitute 5.3.0 — mock DaprClient for host tests
- **Integration testing**: `Microsoft.AspNetCore.Mvc.Testing` with `WebApplicationFactory<Program>` — the `public partial class Program;` declaration enables this
- **Tier**: These are Tier 1 tests (no DAPR sidecar needed — mock DaprClient)

### Previous Story Intelligence

**Story 14-3** (in review) created:
- 7 REST controllers under `api/v1/admin/` prefix
- 4 authorization infrastructure classes (policies, claim types, claims transformation, tenant filter)
- 4 request DTOs
- `AddAdminApi()` extension method that registers auth policies, claims transformation, tenant filter, and calls `AddAdminServer()`
- 154 tests in `Hexalith.EventStore.Admin.Server.Tests/`
- Debug notes: WebApplicationFactory requires entry point → the `public partial class Program;` pattern solves this
- Admin.Server remains class library with `FrameworkReference` for ASP.NET Core types

**Story 14-2** created:
- 10 DAPR-backed service implementations
- `AdminServerOptions` (state store name, app IDs, timeouts)
- `AddAdminServer()` DI registration
- `IAdminAuthContext` + `NullAdminAuthContext` for JWT token forwarding

**Story 14-1** created:
- 10 service interfaces in Admin.Abstractions
- 30+ DTOs with `ToString()` redaction
- `AdminRole` enum: ReadOnly, Operator, Admin

### Git Intelligence

Recent commits focus on:
- Unit tests for DAPR services (projection, storage, stream, tenant, type catalog)
- Model tests for Admin.Abstractions
- MessageId validation tests
- All following xUnit + Shouldly + NSubstitute patterns

### Project Structure After This Story

```
src/Hexalith.EventStore.Admin.Server.Host/     ← NEW
  Hexalith.EventStore.Admin.Server.Host.csproj
  Program.cs
  Properties/launchSettings.json
  appsettings.json
  appsettings.Development.json

src/Hexalith.EventStore.Aspire/                ← MODIFIED
  HexalithEventStoreExtensions.cs              (add adminServer parameter)
  HexalithEventStoreResources.cs               (add AdminServer property)

src/Hexalith.EventStore.AppHost/               ← MODIFIED
  Program.cs                                   (add admin-server resource + Keycloak wiring)
  Hexalith.EventStore.AppHost.csproj           (add Admin.Server.Host project reference)
  DaprComponents/accesscontrol.yaml            (add admin-server policy)

tests/Hexalith.EventStore.Admin.Server.Host.Tests/  ← NEW
  Hexalith.EventStore.Admin.Server.Host.Tests.csproj
  HostBootstrapTests.cs
  MiddlewareOrderTests.cs
```

### References

- [Source: _bmad-output/planning-artifacts/architecture.md — ADR-P4: Three-Interface Architecture Over Single DAPR API]
- [Source: _bmad-output/planning-artifacts/architecture.md — DAPR Building Block Dependencies (Service Invocation v2)]
- [Source: _bmad-output/planning-artifacts/architecture.md — D4: DAPR Access Control Per-App-ID Allow List]
- [Source: _bmad-output/planning-artifacts/architecture.md — Package Dependency Boundaries]
- [Source: _bmad-output/planning-artifacts/architecture.md — NuGet Package Architecture (Admin.Server = Aspire resource)]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-03-21-admin-tooling.md — Story 14-4 scope]
- [Source: _bmad-output/implementation-artifacts/14-3-admin-server-rest-api-controllers-with-jwt-auth.md — AddAdminApi(), host responsibilities, DO NOT list]
- [Source: _bmad-output/implementation-artifacts/14-5-admin-api-openapi-spec-and-swagger-ui.md — Expects host from 14-4]
- [Source: src/Hexalith.EventStore.CommandApi/Program.cs — Host bootstrap pattern]
- [Source: src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs — Aspire resource registration]
- [Source: src/Hexalith.EventStore.AppHost/Program.cs — Aspire orchestration topology]
- [Source: src/Hexalith.EventStore.AppHost/DaprComponents/accesscontrol.yaml — DAPR access control policies]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
