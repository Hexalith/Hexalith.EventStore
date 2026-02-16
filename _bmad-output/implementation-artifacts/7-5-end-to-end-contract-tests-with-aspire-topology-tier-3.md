# Story 7.5: End-to-End Contract Tests with Aspire Topology (Tier 3)

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a **developer**,
I want end-to-end contract tests that validate the full command lifecycle across the complete Aspire topology (CommandApi -> Actor -> Domain Service -> State Store -> Pub/Sub),
so that I can verify the entire system works correctly before release (FR47).

## Acceptance Criteria

1. **Aspire test host configuration** - The IntegrationTests project uses `DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_EventStore_AppHost>()` to start the full Aspire topology (CommandApi, sample domain service, Redis, Dapr sidecars)
2. **Full command lifecycle verification** - Tests submit commands via the REST API and verify the complete lifecycle: 202 Accepted -> status tracking -> events persisted -> events published -> Completed
3. **JWT authentication and authorization flow** - Tests verify JWT authentication and authorization flow (using symmetric key `TestJwtTokenGenerator` for fast tests, and optionally Keycloak real OIDC tokens for E2E security tests per D11)
4. **RFC 7807 error responses** - Tests verify RFC 7807 ProblemDetails error responses for invalid/unauthorized requests
5. **Dead-letter routing verification** - Tests verify dead-letter routing for simulated failures
6. **Infrastructure portability** - Tests verify infrastructure portability (same tests pass on Redis; PostgreSQL config swap validation is structural/documented, not runtime in Tier 3 scope)

## Tasks / Subtasks

- [ ] Task 1: Create AspireTestFixture with DistributedApplicationTestingBuilder (AC: #1)
  - [ ] 1.1 Implement xUnit `IAsyncLifetime` fixture using `DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_EventStore_AppHost>()`
  - [ ] 1.2 Configure test logging (Debug level for app, Warning for Aspire infrastructure)
  - [ ] 1.3 Configure `HttpClientDefaults` with standard resilience handler
  - [ ] 1.4 Build and start the distributed application, wait for `commandapi` resource to be healthy
  - [ ] 1.5 Expose `HttpClient` factory for `commandapi` resource via `app.CreateHttpClient("commandapi")`
  - [ ] 1.6 Create xUnit `[CollectionDefinition("AspireTopology")]` collection fixture to share topology across tests
  - [ ] 1.7 Add `TestJwtTokenGenerator` helper to create valid JWT tokens with tenant/domain/permission claims (reuse from existing Testing package or create if needed)
- [ ] Task 2: Command lifecycle end-to-end tests (AC: #2)
  - [ ] 2.1 Test POST /api/v1/commands with valid IncrementCounter command -> 202 Accepted with correlation ID
  - [ ] 2.2 Test GET /api/v1/commands/{id}/status -> returns command status tracking through stages to Completed
  - [ ] 2.3 Test full lifecycle: submit command -> poll status -> verify events persisted (via second command proving state was updated) -> Completed
  - [ ] 2.4 Test multiple sequential commands: IncrementCounter x3 -> DecrementCounter -> verify counter state = 2 via IncrementCounter (state reflects all prior events)
  - [ ] 2.5 Test ResetCounter command -> verify counter state resets
  - [ ] 2.6 Test DecrementCounter on zero counter -> CounterCannotGoNegative rejection event -> verify rejection is recorded
- [ ] Task 3: JWT authentication and authorization tests (AC: #3)
  - [ ] 3.1 Test request without JWT token -> 401 Unauthorized
  - [ ] 3.2 Test request with valid JWT but missing `command:submit` permission -> 403 Forbidden
  - [ ] 3.3 Test request with valid JWT including correct tenant, domain, permissions -> 202 Accepted
  - [ ] 3.4 Test request with JWT for wrong tenant -> tenant validation rejection
  - [ ] 3.5 (Optional, E2E trait) Test with real Keycloak OIDC token if Keycloak enabled (D11, Rule #16)
- [ ] Task 4: RFC 7807 error response tests (AC: #4)
  - [ ] 4.1 Test malformed JSON body -> 400 Bad Request with ProblemDetails
  - [ ] 4.2 Test missing required fields -> 400 Bad Request with validation errors in ProblemDetails
  - [ ] 4.3 Test unauthorized request -> 401 with ProblemDetails containing correlationId
  - [ ] 4.4 Test forbidden request -> 403 with ProblemDetails
  - [ ] 4.5 Verify all error responses include `correlationId` and `tenantId` extension fields where applicable
- [ ] Task 5: Dead-letter routing tests (AC: #5)
  - [ ] 5.1 Test command to non-existent domain service -> dead-letter routing triggered
  - [ ] 5.2 Verify dead-letter includes full context (original command, failure reason, correlation ID)
- [ ] Task 6: Infrastructure portability documentation (AC: #6)
  - [ ] 6.1 Document in test file comments how to swap Redis for PostgreSQL via Dapr component config
  - [ ] 6.2 Verify test design is backend-agnostic (no Redis-specific assertions)

## Dev Notes

### Architecture Constraints

- **Three-Tier Test Architecture:** This story implements **Tier 3** only. Tier 3 tests live in `tests/Hexalith.EventStore.IntegrationTests/` and test the FULL Aspire topology via REST API (CommandApi -> Actor -> Domain Service -> State Store -> Pub/Sub)
- **Aspire Testing Pattern:** Use `DistributedApplicationTestingBuilder.CreateAsync<Projects.Hexalith_EventStore_AppHost>()` from `Aspire.Hosting.Testing` package (already referenced in csproj). This launches the full AppHost topology including Redis, Dapr sidecars, CommandApi, and sample domain service
- **HttpClient Creation:** Use `app.CreateHttpClient("commandapi")` to get an HttpClient pre-configured with the correct base URL for the CommandApi resource
- **Resource Health Waiting:** Use `app.ResourceNotifications.WaitForResourceHealthyAsync("commandapi", cancellationToken)` before sending requests
- **5-Step Actor Delegation Pattern:** Full pipeline exercised end-to-end: IdempotencyChecker -> TenantValidator -> EventStreamReader -> DomainServiceInvoker -> ActorStateMachine
- **State Machine Stages:** `Received -> Processing -> EventsStored -> EventsPublished -> Completed | Rejected | PublishFailed | TimedOut`
- **Rule #4:** NEVER add custom retry logic -- Dapr resiliency only
- **Rule #14:** Dapr sidecar call timeout is 5 seconds
- **Rule #16:** E2E security tests use real Keycloak OIDC tokens -- never synthetic JWTs for runtime security verification. `TestJwtTokenGenerator` (HS256) is for fast unit/integration tests only

### Technical Stack

| Technology | Version | Notes |
|-----------|---------|-------|
| .NET SDK | 10.0.102 | Target framework `net10.0` |
| C# | 14 | Language features |
| Aspire | 13.1.0 | `Aspire.Hosting.Testing` for `DistributedApplicationTestingBuilder` |
| CommunityToolkit.Aspire.Hosting.Dapr | 9.7.0 | Aspire + Dapr integration |
| Dapr Runtime | 1.16.6 | Latest stable |
| Dapr .NET SDK | Dapr.Client 1.16.0, Dapr.AspNetCore 1.16.1 | Actor support |
| xUnit | Latest | Test framework (already in IntegrationTests) |
| Shouldly | Latest | Assertions (already in IntegrationTests) |
| Microsoft.AspNetCore.Mvc.Testing | Latest | HTTP testing (already in IntegrationTests) |

### Testing Patterns (Follow Established Conventions from Story 7.4)

- **Test naming:** `{Method}_{Scenario}_{ExpectedResult}` (e.g., `SubmitCommand_ValidIncrementCounter_Returns202Accepted`)
- **AAA pattern:** Explicit `// Arrange`, `// Act`, `// Assert` sections
- **Assertions:** Shouldly fluent syntax (`result.ShouldBe(expected)`, `response.StatusCode.ShouldBe(HttpStatusCode.Accepted)`)
- **Collection fixture:** Share Aspire topology across all integration tests via `[Collection("AspireTopology")]`
- **Timeout:** Use `TimeSpan.FromSeconds(30)` default timeout with `.WaitAsync()` pattern
- **JWT tokens:** Generate test JWT tokens with claims: `tenants` (JSON array), `domains` (JSON array), `permissions` (JSON array) matching `EventStoreClaimsTransformation` expectations

### Aspire Testing API Usage Pattern

```csharp
// Fixture setup
var appHost = await DistributedApplicationTestingBuilder
    .CreateAsync<Projects.Hexalith_EventStore_AppHost>(cancellationToken);

appHost.Services.AddLogging(logging =>
{
    logging.SetMinimumLevel(LogLevel.Debug);
    logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
    logging.AddFilter("Aspire.", LogLevel.Warning);
});

appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
{
    clientBuilder.AddStandardResilienceHandler();
});

await using var app = await appHost.BuildAsync(cancellationToken);
await app.StartAsync(cancellationToken);

// Test usage
using var httpClient = app.CreateHttpClient("commandapi");
await app.ResourceNotifications
    .WaitForResourceHealthyAsync("commandapi", cancellationToken)
    .WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
```

### Sample Domain Service (Test Subject)

Use the Counter domain from Story 7.1 as the E2E test subject:
- **Commands:** `IncrementCounter`, `DecrementCounter`, `ResetCounter`
- **Events:** `CounterIncremented`, `CounterDecremented`, `CounterReset`, `CounterCannotGoNegative` (rejection)
- **State:** `CounterState` with `CurrentValue` property
- **Processor:** `CounterProcessor : IDomainProcessor` -- pure function contract

### Command API Endpoints

- `POST /api/v1/commands` -- Submit command (requires JWT with `command:submit` permission)
- `GET /api/v1/commands/{id}/status` -- Query status (requires JWT with `command:query` permission)
- `POST /api/v1/commands/{id}/replay` -- Replay command (requires JWT with `command:replay` permission)

### JWT Token Structure for Tests

```json
{
  "sub": "test-user",
  "iss": "test-issuer",
  "aud": "hexalith-eventstore",
  "tenants": ["tenant-a"],
  "domains": ["counter"],
  "permissions": ["command:submit", "command:query", "command:replay"]
}
```

### Key Test Scenarios

1. **Full Command Lifecycle (FR47):** POST IncrementCounter -> 202 Accepted -> GET status -> Completed -> verify state change via subsequent command behavior
2. **Rejection Flow:** DecrementCounter on zero counter -> CounterCannotGoNegative rejection -> verify rejection recorded in status
3. **Auth Pipeline (FR31, FR32):** No token -> 401, wrong permissions -> 403, wrong tenant -> rejection, valid token -> 202
4. **RFC 7807 (D5):** All error responses return `application/problem+json` with required fields
5. **Dead-Letter (FR18):** Command to unregistered domain -> dead-letter routing with full context

### Project Structure Notes

```
tests/Hexalith.EventStore.IntegrationTests/
├── Hexalith.EventStore.IntegrationTests.csproj  ← Existing (has AppHost + Aspire.Hosting.Testing refs)
├── BuildVerificationTests.cs                     ← Existing
├── Fixtures/                                     ← NEW directory
│   └── AspireTestFixture.cs                      ← NEW: AC #1 Aspire topology fixture
├── CommandLifecycleTests.cs                      ← NEW: AC #2 full lifecycle
├── AuthenticationTests.cs                        ← NEW: AC #3 JWT auth flow
├── ErrorResponseTests.cs                         ← NEW: AC #4 RFC 7807
├── DeadLetterTests.cs                            ← NEW: AC #5 dead-letter routing
└── Helpers/                                      ← NEW directory
    └── TestJwtTokenGenerator.cs                  ← NEW: JWT token helper (if not in Testing package)
```

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 7, Story 7.5]
- [Source: _bmad-output/planning-artifacts/architecture.md#Testing Architecture, D1 Event Storage, D5 Error Handling, D11 Keycloak, Enforcement Rules]
- [Source: _bmad-output/implementation-artifacts/7-4-integration-tests-with-dapr-test-containers-tier-2.md#Previous Story Learnings]
- [Source: tests/Hexalith.EventStore.IntegrationTests/ -- Existing test project with Aspire.Hosting.Testing]
- [Source: src/Hexalith.EventStore.AppHost/Program.cs -- Full topology definition]
- [Source: src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs -- AddHexalithEventStore topology wiring]
- [Source: https://learn.microsoft.com/dotnet/aspire/testing/ -- Aspire testing documentation]

### Previous Story Intelligence

**From Story 7.4 (Integration Tests with Dapr Test Containers - Tier 2):**
- `DaprTestContainerFixture` with `IAsyncLifetime` pattern established -- reuse same pattern for `AspireTestFixture`
- `[Collection("DaprTestContainer")]` collection fixture pattern -- mirror with `[Collection("AspireTopology")]`
- Test naming convention `{Method}_{Scenario}_{ExpectedResult}` established
- Shouldly assertions and AAA pattern established
- FakeDomainServiceInvoker and FakeEventPublisher used in Tier 2 -- Tier 3 uses REAL implementations via full Aspire topology
- Build succeeds with 785 passing tests, 1 pre-existing failure (SecretsProtectionTests -- unrelated)
- `FrameworkReference` for ASP.NET Core already added to Server.Tests (may need similar in IntegrationTests)
- Counter domain service (commands, events, state, processor) fully implemented and tested in Tier 2

**From Story 7.3 (Production Dapr Component Configurations):**
- Component names (`statestore`, `pubsub`) are identical across environments -- tests should be backend-agnostic
- NFR29: Zero code changes when swapping backends -- test design must not depend on Redis-specific behavior

### Git Intelligence

Recent commits show:
- Stories 6.1-6.4 focused on observability, telemetry, and health check endpoints
- Structured logging and health check patterns established
- Dapr health check endpoint implementation provides patterns for resource health waiting
- JWT authentication and authorization behaviors implemented in CommandApi
- Authorization policies and claims transformation fully working

### Latest Tech Information

**Aspire.Hosting.Testing (v13.1.0):**
- Use `DistributedApplicationTestingBuilder.CreateAsync<TAppHost>()` -- static factory method
- Returns `IDistributedApplicationTestingBuilder` which implements `IDistributedApplicationBuilder`
- Call `appHost.BuildAsync()` -> `app.StartAsync()` -> `app.CreateHttpClient("resourceName")`
- Wait for resource health with `app.ResourceNotifications.WaitForResourceHealthyAsync("resourceName", ct)`
- Use `.WaitAsync(TimeSpan, ct)` to apply timeouts to async operations
- AppHost reference in csproj needs `<IsAspireProjectResource>false</IsAspireProjectResource>` (already configured)
- Configure `HttpClientDefaults` with `AddStandardResilienceHandler()` for resilient test HTTP calls

### Out of Scope

- Performance benchmarking (NFR1-NFR8 targets)
- CI/CD pipeline setup (Story 7.6 -- pipeline will run these tests)
- Aspire publisher deployment manifests (Story 7.7)
- Domain service hot reload validation (Story 7.8)
- Runtime PostgreSQL/CosmosDB backend swap testing (structural validation only -- runtime portability is inherent via Dapr component abstraction)

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
