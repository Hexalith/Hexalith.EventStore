# Story 2.4: JWT Authentication & Claims Transformation

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an **API consumer**,
I want the CommandApi to authenticate my requests via JWT token (signature, expiry, issuer validation) and transform claims into tenant/domain/command-type permissions,
So that only authenticated consumers can submit commands (FR30).

## Acceptance Criteria

1. **401 for missing/invalid/expired JWT** - Given the CommandApi has JWT authentication middleware configured, When I submit a request without a JWT token or with an invalid/expired token, Then the response is `401 Unauthorized` as RFC 7807 ProblemDetails with `correlationId` extension and human-readable `detail` message (no stack traces per enforcement rule #13).

2. **Failed auth logging without JWT token** - When authentication fails, Then the failure is logged at `Warning` level with: correlationId, source IP (`HttpContext.Connection.RemoteIpAddress`), request path, exception type and message. The JWT token itself MUST NOT appear in any log output (NFR11). The `Payload` and `Extensions` of any parsed claims MUST NOT be logged.

3. **JWT validated every request** - When I submit a request with a valid JWT token, Then the token is validated for signature, expiry (`exp`), and issuer (`iss`) on every request (NFR10). Token validation uses `Microsoft.AspNetCore.Authentication.JwtBearer` middleware with `TokenValidationParameters` configured for `ValidateIssuer`, `ValidateAudience`, `ValidateIssuerSigningKey`, and `ValidateLifetime`.

4. **IClaimsTransformation extracts permissions** - After successful JWT authentication, Then `IClaimsTransformation` extracts tenant, domain, and command type permissions from JWT claims and normalizes them into `eventstore:tenant`, `eventstore:domain`, and `eventstore:permission` claims on the `ClaimsPrincipal`. The transformation is idempotent (safe to run multiple times per request).

5. **[Authorize] on CommandsController** - The `CommandsController` requires authentication via `[Authorize]` attribute (security layer 3). Unauthenticated requests are rejected before entering the MediatR pipeline. Health check endpoints (`/health`, `/alive`) remain accessible without authentication.

## Tasks / Subtasks

- [ ] Task 1: Configure JWT Bearer Authentication in ServiceCollectionExtensions (AC: #1, #3)
  - [ ] 1.1 Create `Authentication/EventStoreAuthenticationOptions.cs` record with properties: `Authority` (string), `Audience` (string), `Issuer` (string), `SigningKey` (string), `RequireHttpsMetadata` (bool)
  - [ ] 1.2 In `AddCommandApi()`, replace auth stubs with `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => ...)` configured from `EventStoreAuthenticationOptions` bound from `Authentication:JwtBearer` config section
  - [ ] 1.3 Configure `TokenValidationParameters`: `ValidateIssuer = true`, `ValidateAudience = true`, `ValidateIssuerSigningKey = true`, `ValidateLifetime = true`, `ClockSkew = TimeSpan.FromMinutes(1)`
  - [ ] 1.4 Support two modes: (a) when `Authority` is set, use OIDC discovery (production); (b) when `SigningKey` is set and `Authority` is empty, use symmetric key validation (development/testing)
  - [ ] 1.5 Set `options.MapInboundClaims = false` to preserve original JWT claim names (avoid namespace mapping)
  - [ ] 1.6 Ensure `ConfigureAwait(false)` on all async calls (CA2007), `ArgumentNullException.ThrowIfNull()` on public methods (CA1062)

- [ ] Task 2: Add JwtBearerEvents for failure logging and ProblemDetails response (AC: #1, #2)
  - [ ] 2.1 Configure `JwtBearerEvents.OnAuthenticationFailed` to log at `Warning` level: correlationId (from `HttpContext.Items["CorrelationId"]`), source IP, request path, exception type, exception message. NEVER log the JWT token itself (NFR11)
  - [ ] 2.2 Configure `JwtBearerEvents.OnChallenge` to return RFC 7807 ProblemDetails with: `Status = 401`, `Title = "Unauthorized"`, `Type = "https://tools.ietf.org/html/rfc9457#section-3"`, `Detail` = human-readable message based on error type, `Instance` = request path, `Extensions` = { `correlationId` }. Call `context.HandleResponse()` to suppress default challenge behavior
  - [ ] 2.3 In `OnChallenge`, attempt to extract tenant from the request body or query params for `tenantId` extension in ProblemDetails (best-effort, do not fail if unavailable)
  - [ ] 2.4 Log failed auth at `Warning` level in `OnChallenge` with correlationId, source IP, error, error description (not the JWT token)

- [ ] Task 3: Implement IClaimsTransformation (AC: #4)
  - [ ] 3.1 Create `Authentication/EventStoreClaimsTransformation.cs` implementing `IClaimsTransformation`
  - [ ] 3.2 In `TransformAsync`, extract from JWT claims: `tenants` (JSON array or space-delimited string), `domains` (JSON array or space-delimited string), `permissions` (JSON array or space-delimited string). Also support singular `tenant_id`/`tid` claims mapped to `eventstore:tenant`
  - [ ] 3.3 Add normalized claims to a new `ClaimsIdentity`: `eventstore:tenant` (one per tenant), `eventstore:domain` (one per domain), `eventstore:permission` (one per permission)
  - [ ] 3.4 Ensure idempotency: check if `eventstore:tenant` claims already exist before adding (transformation may run multiple times per request)
  - [ ] 3.5 Inject `ILogger<EventStoreClaimsTransformation>` and log at `Debug` level: subject, tenant count, domain count (no sensitive data)
  - [ ] 3.6 Register `IClaimsTransformation` in `AddCommandApi()`: `services.AddTransient<IClaimsTransformation, EventStoreClaimsTransformation>()`

- [ ] Task 4: Add [Authorize] to CommandsController and configure anonymous endpoints (AC: #5)
  - [ ] 4.1 Add `[Authorize]` attribute to `CommandsController` class (or to the `Submit` method)
  - [ ] 4.2 Verify `/health` and `/alive` endpoints remain accessible without authentication (they are mapped via `MapDefaultEndpoints()` which doesn't require auth by default -- verify this still works)
  - [ ] 4.3 Verify `UseAuthentication()` and `UseAuthorization()` are in correct order in `Program.cs` (already present)

- [ ] Task 5: Add JWT configuration to appsettings (AC: #3)
  - [ ] 5.1 Add `Authentication:JwtBearer` section to `appsettings.json` with empty/default values
  - [ ] 5.2 Add `Authentication:JwtBearer` section to `appsettings.Development.json` with development symmetric key, issuer "hexalith-dev", audience "hexalith-eventstore", `RequireHttpsMetadata = false`
  - [ ] 5.3 Ensure the signing key in development settings is at least 256 bits (32 characters) for HS256

- [ ] Task 6: Update existing integration tests to use JWT tokens (AC: #1, #3, #5)
  - [ ] 6.1 Create a test helper `TestJwtTokenGenerator` in the integration test project that generates valid JWT tokens with configurable claims using `System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler` and a known symmetric key
  - [ ] 6.2 Configure `WebApplicationFactory` test host with the same symmetric key used by `TestJwtTokenGenerator`
  - [ ] 6.3 Update ALL existing integration tests (from Stories 2.1 and 2.2) to include a valid JWT `Authorization: Bearer {token}` header in requests
  - [ ] 6.4 Verify all existing tests still pass after adding JWT authentication

- [ ] Task 7: Write unit tests for EventStoreClaimsTransformation (AC: #4)
  - [ ] 7.1 `TransformAsync_JwtWithTenantsArray_AddsEventStoreTenantClaims` - verify array of tenants produces correct claims
  - [ ] 7.2 `TransformAsync_JwtWithSingleTenantId_AddsEventStoreTenantClaim` - verify single `tenant_id` claim works
  - [ ] 7.3 `TransformAsync_JwtWithDomainsAndPermissions_AddsNormalizedClaims` - verify domains and permissions
  - [ ] 7.4 `TransformAsync_NoCustomClaims_AddsNoEventStoreClaims` - verify graceful handling of missing custom claims
  - [ ] 7.5 `TransformAsync_AlreadyTransformed_DoesNotDuplicate` - verify idempotency
  - [ ] 7.6 `TransformAsync_NullPrincipal_ThrowsArgumentNullException` - verify CA1062 compliance

- [ ] Task 8: Write integration tests for JWT authentication flow (AC: #1, #2, #3, #5)
  - [ ] 8.1 `PostCommands_NoAuthToken_Returns401ProblemDetails` - verify 401 with ProblemDetails body including correlationId
  - [ ] 8.2 `PostCommands_InvalidToken_Returns401ProblemDetails` - verify malformed token returns 401
  - [ ] 8.3 `PostCommands_ExpiredToken_Returns401ProblemDetails` - verify expired token returns 401
  - [ ] 8.4 `PostCommands_WrongIssuer_Returns401ProblemDetails` - verify wrong issuer returns 401
  - [ ] 8.5 `PostCommands_ValidToken_Returns202Accepted` - verify valid JWT allows command submission
  - [ ] 8.6 `PostCommands_ValidTokenWithTenantClaims_ClaimsTransformedCorrectly` - verify IClaimsTransformation runs
  - [ ] 8.7 `HealthEndpoint_NoAuth_Returns200` - verify health check is accessible without JWT
  - [ ] 8.8 `AliveEndpoint_NoAuth_Returns200` - verify aliveness check is accessible without JWT
  - [ ] 8.9 `PostCommands_AuthFailure_LogsWithoutJwtToken` - verify structured log entry for failed auth contains correlationId and IP but NOT the JWT token

## Dev Notes

### Architecture Compliance

**Six-Layer Authentication Model (Architecture):** Story 2.4 implements layers 1-3 of the six-layer defense in depth:
- **Layer 1: JWT Authentication** - `Microsoft.AspNetCore.Authentication.JwtBearer` middleware validates signature, expiry, issuer on every request
- **Layer 2: Claims Transformation** - `IClaimsTransformation` normalizes JWT claims into `eventstore:*` claims for downstream authorization
- **Layer 3: Endpoint [Authorize]** - `[Authorize]` attribute on `CommandsController` requires authentication before entering MediatR pipeline

Layers 4-6 are handled by subsequent stories:
- Layer 4: MediatR AuthorizationBehavior (Story 2.5)
- Layer 5: Actor-level TenantValidator (Story 3.3)
- Layer 6: DAPR access control policies (Story 5.1)

**Enforcement Rules to Follow:**
- Rule #5: Never log event payload data -- envelope metadata only (SEC-5, NFR12)
- Rule #7: ProblemDetails for all API error responses -- 401 must be ProblemDetails
- Rule #9: correlationId in every structured log entry and OpenTelemetry activity
- Rule #10: Register services via `Add*` extension methods -- never inline in Program.cs
- Rule #13: No stack traces in production error responses -- 401 ProblemDetails must not contain stack trace

**NFR Compliance:**
- NFR9: TLS 1.2+ -- Enforced at deployment/infrastructure level. In development, `RequireHttpsMetadata = false` allows HTTP for local testing. In production, `RequireHttpsMetadata = true` enforces HTTPS for OIDC metadata discovery
- NFR10: JWT validated for signature, expiry, and issuer on EVERY request -- This is the default behavior of JWT Bearer middleware
- NFR11: Failed auth attempts logged with request metadata (source IP, attempted tenant, command type) WITHOUT logging the JWT token itself
- NFR14: Secrets (JWT signing keys) never stored in source control -- Development signing key is a known test key, not a production secret. Production keys come from DAPR config store or environment variables

### Critical Design Decisions

**What Already Exists (from Stories 2.1, 2.2, 2.3):**
- `ServiceCollectionExtensions.AddCommandApi()` with auth stubs: `services.AddAuthentication()` / `services.AddAuthorization()` -- comment says "Story 2.4 will add full JWT"
- `Program.cs` with `app.UseAuthentication()` and `app.UseAuthorization()` already in correct middleware order
- `CorrelationIdMiddleware` runs BEFORE authentication (correct -- generates correlation ID for auth failure logging)
- `ValidationExceptionHandler` and `GlobalExceptionHandler` for RFC 7807 ProblemDetails
- `CommandsController` with POST `/api/v1/commands` -- currently NO `[Authorize]` attribute
- `Microsoft.AspNetCore.Authentication.JwtBearer` package already referenced in CommandApi.csproj (v10.0.0)
- OpenTelemetry tracing configured in ServiceDefaults with ASP.NET Core instrumentation
- Integration tests using `WebApplicationFactory<Program>` with extern alias

**What Story 2.4 Adds:**
1. **`EventStoreAuthenticationOptions`** -- Configuration record for JWT settings (authority, audience, issuer, signing key)
2. **Full JWT Bearer configuration** -- Replace auth stubs with configured `AddJwtBearer()` with `TokenValidationParameters`
3. **`EventStoreClaimsTransformation`** -- `IClaimsTransformation` that normalizes JWT claims into `eventstore:*` claims
4. **`JwtBearerEvents` handlers** -- `OnAuthenticationFailed` for logging, `OnChallenge` for ProblemDetails 401 response
5. **`[Authorize]` on CommandsController** -- Enforces authentication at endpoint level (layer 3)
6. **JWT configuration** in `appsettings.json` / `appsettings.Development.json`
7. **`TestJwtTokenGenerator`** -- Test helper for generating valid JWT tokens in integration tests
8. **Updated existing tests** -- All existing integration tests updated to include JWT tokens

**Two-Mode JWT Configuration:**
The JWT authentication supports two modes based on configuration:
- **Development/Testing mode:** When `Authentication:JwtBearer:SigningKey` is provided and `Authority` is empty, uses symmetric key validation (`SymmetricSecurityKey` with `HmacSha256`). This allows test token generation without an external identity provider
- **Production mode:** When `Authentication:JwtBearer:Authority` is provided, uses OIDC discovery to fetch signing keys from the identity provider's `.well-known/openid-configuration` endpoint

```csharp
// Decision logic pseudocode:
if (!string.IsNullOrEmpty(options.Authority))
{
    // Production: OIDC discovery
    jwtOptions.Authority = options.Authority;
    jwtOptions.RequireHttpsMetadata = options.RequireHttpsMetadata;
}
else if (!string.IsNullOrEmpty(options.SigningKey))
{
    // Development: symmetric key
    jwtOptions.TokenValidationParameters.IssuerSigningKey =
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
}
```

**JWT Claims Convention:**
The system expects JWT tokens with the following custom claims (all optional, but needed for authorization in Story 2.5):

| JWT Claim Name | Type | Description | Example |
|---------------|------|-------------|---------|
| `sub` | string | Subject (user ID) | `"user-123"` |
| `tenants` | string[] or space-delimited | Authorized tenant IDs | `["tenant-a", "tenant-b"]` |
| `tenant_id` or `tid` | string | Single tenant ID (alternative to array) | `"tenant-a"` |
| `domains` | string[] or space-delimited | Authorized domains | `["orders", "inventory"]` |
| `permissions` | string[] or space-delimited | Command type permissions | `["commands:*"]` |

**IClaimsTransformation Output:**
After transformation, the `ClaimsPrincipal` will have these normalized claims added:

| Claim Type | Value | Source |
|-----------|-------|--------|
| `eventstore:tenant` | One claim per tenant | `tenants` array, `tenant_id`, or `tid` |
| `eventstore:domain` | One claim per domain | `domains` array |
| `eventstore:permission` | One claim per permission | `permissions` array |

**ProblemDetails for 401:**
The `OnChallenge` handler produces a ProblemDetails response matching the established pattern:
```json
{
  "type": "https://tools.ietf.org/html/rfc9457#section-3",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Authentication is required to access this resource.",
  "instance": "/api/v1/commands",
  "correlationId": "abc-123-def-456"
}
```

For expired tokens:
```json
{
  "detail": "The provided authentication token has expired."
}
```

For invalid tokens:
```json
{
  "detail": "The provided authentication token is invalid."
}
```

### Technical Requirements

**Existing Types to Use:**
- `CorrelationIdMiddleware.HttpContextKey` (constant `"CorrelationId"`) -- access correlation ID in JwtBearerEvents handlers
- `ProblemDetails` from `Microsoft.AspNetCore.Mvc` -- RFC 7807 response format
- `JwtBearerDefaults.AuthenticationScheme` from `Microsoft.AspNetCore.Authentication.JwtBearer` -- default scheme name `"Bearer"`
- `TokenValidationParameters` from `Microsoft.IdentityModel.Tokens` -- configure validation rules
- `IClaimsTransformation` from `Microsoft.AspNetCore.Authentication` -- claims transformation interface
- `HttpContext.Items["CorrelationId"]` -- correlation ID set by CorrelationIdMiddleware

**NuGet Packages Already Available (in Directory.Packages.props):**
- `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.0 -- JWT Bearer middleware, `JwtBearerEvents`, `JwtBearerOptions`
- `Microsoft.IdentityModel.Tokens` -- Included transitively via JwtBearer, provides `TokenValidationParameters`, `SymmetricSecurityKey`
- `System.IdentityModel.Tokens.Jwt` -- Included transitively via JwtBearer, provides `JwtSecurityTokenHandler` for test token generation
- `MediatR` 14.0.0 -- existing pipeline
- `Shouldly` 4.3.0 -- test assertions
- `Microsoft.AspNetCore.Mvc.Testing` 10.0.0 -- WebApplicationFactory for integration tests
- `NSubstitute` 5.3.0 -- available for mocking if needed

**No new NuGet packages needed.** All dependencies are already available through `Microsoft.AspNetCore.Authentication.JwtBearer` and its transitive dependencies.

### Library & Framework Requirements

| Library | Version | Purpose |
|---------|---------|---------|
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.0 | JWT Bearer middleware, JwtBearerEvents, JwtBearerOptions |
| Microsoft.IdentityModel.Tokens | transitive | TokenValidationParameters, SymmetricSecurityKey, SecurityTokenExpiredException |
| System.IdentityModel.Tokens.Jwt | transitive | JwtSecurityTokenHandler (test token generation) |

No new NuGet packages needed. All dependencies are already available.

### File Structure Requirements

**New files to create:**
```
src/Hexalith.EventStore.CommandApi/
├── Authentication/
│   ├── EventStoreAuthenticationOptions.cs     # NEW: JWT configuration record
│   └── EventStoreClaimsTransformation.cs      # NEW: IClaimsTransformation implementation

tests/Hexalith.EventStore.IntegrationTests/
├── Helpers/
│   └── TestJwtTokenGenerator.cs               # NEW: Test JWT token generator
├── CommandApi/
│   └── JwtAuthenticationIntegrationTests.cs   # NEW: Integration tests for JWT flow
```

**Existing files to modify:**
```
src/Hexalith.EventStore.CommandApi/
├── Extensions/
│   └── ServiceCollectionExtensions.cs         # MODIFY: Replace auth stubs with full JWT config
├── Controllers/
│   └── CommandsController.cs                  # MODIFY: Add [Authorize] attribute
├── appsettings.json                           # MODIFY: Add Authentication:JwtBearer section
├── appsettings.Development.json               # MODIFY: Add dev JWT config with symmetric key

tests/Hexalith.EventStore.IntegrationTests/
├── CommandApi/
│   └── *.cs                                   # MODIFY: All existing integration tests need JWT tokens
```

**Existing files to verify (no changes expected):**
```
src/Hexalith.EventStore.CommandApi/
├── Program.cs                                 # VERIFY: UseAuthentication/UseAuthorization already correct
├── Middleware/
│   └── CorrelationIdMiddleware.cs             # VERIFY: Runs before auth (already correct in Program.cs)
├── ErrorHandling/
│   └── GlobalExceptionHandler.cs              # VERIFY: Still handles 500s correctly
│   └── ValidationExceptionHandler.cs          # VERIFY: Still handles 400s correctly

src/Hexalith.EventStore.ServiceDefaults/
└── Extensions.cs                              # VERIFY: Health endpoints remain accessible without auth
```

### Testing Requirements

**Test Projects:**
- `tests/Hexalith.EventStore.Server.Tests/` -- Unit tests for EventStoreClaimsTransformation
- `tests/Hexalith.EventStore.IntegrationTests/` -- Integration tests for JWT flow + updated existing tests

**Test Patterns (established in Stories 1.6, 2.1, 2.2, 2.3):**
- Method naming: `{Method}_{Scenario}_{ExpectedResult}`
- Arrange/Act/Assert pattern
- Shouldly for assertions
- `WebApplicationFactory<Program>` with extern alias for integration tests
- `ConfigureAwait(false)` on all async test methods

**Test JWT Token Generation Strategy:**
Create a `TestJwtTokenGenerator` helper class that:
1. Uses a known symmetric key (same key configured in test `WebApplicationFactory`)
2. Generates tokens with `JwtSecurityTokenHandler`
3. Allows setting custom claims (tenants, domains, permissions)
4. Allows setting expiry (for expired token tests)
5. Allows setting issuer/audience (for wrong issuer tests)

```csharp
// Example usage in tests:
var token = TestJwtTokenGenerator.GenerateToken(
    tenants: ["test-tenant"],
    domains: ["test-domain"],
    expires: DateTime.UtcNow.AddHours(1));

client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
```

**WebApplicationFactory Configuration for JWT Tests:**
Configure the test host to use the same symmetric key:
```csharp
builder.ConfigureAppConfiguration((context, config) =>
{
    config.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Authentication:JwtBearer:Issuer"] = TestJwtTokenGenerator.Issuer,
        ["Authentication:JwtBearer:Audience"] = TestJwtTokenGenerator.Audience,
        ["Authentication:JwtBearer:SigningKey"] = TestJwtTokenGenerator.SigningKey,
        ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
    });
});
```

**Minimum Tests (15):**

Unit Tests (6):
1. `TransformAsync_JwtWithTenantsArray_AddsEventStoreTenantClaims`
2. `TransformAsync_JwtWithSingleTenantId_AddsEventStoreTenantClaim`
3. `TransformAsync_JwtWithDomainsAndPermissions_AddsNormalizedClaims`
4. `TransformAsync_NoCustomClaims_AddsNoEventStoreClaims`
5. `TransformAsync_AlreadyTransformed_DoesNotDuplicate`
6. `TransformAsync_NullPrincipal_ThrowsArgumentNullException`

Integration Tests (9):
7. `PostCommands_NoAuthToken_Returns401ProblemDetails`
8. `PostCommands_InvalidToken_Returns401ProblemDetails`
9. `PostCommands_ExpiredToken_Returns401ProblemDetails`
10. `PostCommands_WrongIssuer_Returns401ProblemDetails`
11. `PostCommands_ValidToken_Returns202Accepted`
12. `PostCommands_ValidTokenWithTenantClaims_ClaimsTransformedCorrectly`
13. `HealthEndpoint_NoAuth_Returns200`
14. `AliveEndpoint_NoAuth_Returns200`
15. `PostCommands_AuthFailure_LogsWithoutJwtToken`

**CRITICAL: Update Existing Integration Tests:**
All existing integration tests from Stories 2.1 and 2.2 MUST be updated to include valid JWT tokens. Without this, they will fail with 401 after Story 2.4. The `TestJwtTokenGenerator` helper should be used across all test classes.

Current test count: ~241 tests (after Story 2.3). Story 2.4 should add ~15 new tests bringing total to ~256.

### Previous Story Intelligence

**From Story 2.3 (MediatR Pipeline & Logging Behavior):**
- Created LoggingBehavior as outermost MediatR pipeline behavior
- Created EventStoreActivitySources with custom ActivitySource
- Pipeline order enforced: LoggingBehavior -> ValidationBehavior -> AuthorizationBehavior -> CommandHandler
- AuthorizationBehavior position reserved but NOT yet implemented (Story 2.5)
- Correlation ID access pattern: `IHttpContextAccessor` -> `HttpContext.Items["CorrelationId"]`
- OpenTelemetry ActivitySource registered: `.AddSource("Hexalith.EventStore.CommandApi")`

**From Story 2.2 (Command Validation & RFC 7807 Error Responses):**
- Established IExceptionHandler pattern for ProblemDetails responses
- ValidationExceptionHandler (400) and GlobalExceptionHandler (500) handle all error scenarios
- ProblemDetails always include `correlationId` extension and optionally `tenantId`
- Content type: `application/problem+json`
- ProblemDetails `type` URI: `https://tools.ietf.org/html/rfc9457#section-3`
- Tenant extracted from `HttpContext.Items["RequestTenantId"]` for error context
- All 231 tests pass (9 Client + 48 Testing + 147 Contracts + 10 Server + 17 Integration)

**From Story 2.1 (CommandApi Host & Minimal Endpoint Scaffolding):**
- WebApplicationFactory with extern alias pattern for integration tests
- `CorrelationIdMiddleware` generates GUID correlation IDs, stores in `HttpContext.Items["CorrelationId"]`
- CorrelationIdMiddleware runs FIRST in middleware pipeline (before exception handler, before auth)
- Controller currently has manual entry/exit logging (may be simplified by Story 2.3)

**Key Patterns Established:**
- Primary constructors for DI injection: `public class Foo(IDep dep) : Base`
- Records for immutable data: `public record SubmitCommand(...)`
- `ConfigureAwait(false)` on all async calls (CA2007 compliance)
- `ArgumentNullException.ThrowIfNull()` on all public methods (CA1062 compliance)
- Extern alias for WebApplicationFactory tests to resolve ambiguous `Program` type
- RFC 7807 ProblemDetails with `application/problem+json` content type
- Correlation ID propagated via HttpContext.Items

### Git Intelligence

**Recent Commits (Last 5):**
- `489e959` Merge pull request #22 from Hexalith/feature/story-2.2-command-validation-rfc7807
- `d3f19fd` Story 2.2: Command Validation & RFC 7807 Error Responses
- `abd5f73` Update Claude Code local settings with tool permissions
- `2dce6f8` Merge pull request #20 from Hexalith/feature/story-2.1-commandapi-and-2.2-story
- `85fd090` Story 2.1: CommandApi Host & Minimal Endpoint Scaffolding + Story 2.2 context

**Patterns:**
- Feature branches named `feature/story-X.Y-description`
- PR-based workflow with merge commits
- Commit messages follow "Story X.Y: Title" format
- Stories build incrementally on main branch

**Key Code Conventions from Recent Commits:**
- Primary constructors for DI injection
- Records for immutable data
- Minimal controller pattern: inject IMediator, delegate to MediatR
- Feature folder organization: Pipeline/, ErrorHandling/, Validation/, Middleware/, Models/
- `namespace Hexalith.EventStore.CommandApi.{Feature};` pattern

### Project Structure Notes

**Alignment with Architecture:**
- `Authentication/` is a NEW feature folder in CommandApi, following the feature folder convention (enforcement rule #2)
- `EventStoreAuthenticationOptions` follows the `*Options.cs` record type pattern for configuration
- `EventStoreClaimsTransformation` follows the naming convention: `EventStore` prefix + descriptive name
- Registration in `AddCommandApi()` per enforcement rule #10
- Test helpers in `Helpers/` folder within integration test project

**Dependency Graph Relevant to This Story:**
```
CommandApi -> Server -> Contracts
CommandApi -> ServiceDefaults
CommandApi -> Microsoft.AspNetCore.Authentication.JwtBearer (already referenced)
Tests: IntegrationTests -> CommandApi (via WebApplicationFactory)
Tests: Server.Tests -> Server + CommandApi
```

**Current Test Count:** ~241 tests (after Story 2.3). Story 2.4 should add approximately 15 new tests bringing total to ~256. Additionally, existing integration tests (~17) must be updated with JWT tokens.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 2.4: JWT Authentication & Claims Transformation]
- [Source: _bmad-output/planning-artifacts/architecture.md#Authentication & Security - D4]
- [Source: _bmad-output/planning-artifacts/architecture.md#Security-Critical Architectural Constraints - SEC-2, SEC-3]
- [Source: _bmad-output/planning-artifacts/architecture.md#Communication Patterns - Six-Layer Auth]
- [Source: _bmad-output/planning-artifacts/architecture.md#Enforcement Guidelines - Rules #5, #7, #9, #10, #13]
- [Source: _bmad-output/planning-artifacts/architecture.md#Data Flow - JWT Authentication (layer 1) + Claims Transformation (layer 2)]
- [Source: _bmad-output/implementation-artifacts/2-3-mediatr-pipeline-and-logging-behavior.md]
- [Source: _bmad-output/implementation-artifacts/2-2-command-validation-and-rfc-7807-error-responses.md]
- [Ref: Microsoft.AspNetCore.Authentication.JwtBearer 10.0.0 - https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication]
- [Ref: IClaimsTransformation - https://learn.microsoft.com/en-us/aspnet/core/security/authentication/claims]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
