# Story 16.5: Tenant Management — EventStore Pipeline Integration (Rework)

Status: in-progress

Size: Medium — ~15 files modified, 6 task groups, 9 ACs (~8-12 hours). Corrective rework per SCP-2026-04-06: rewire tenant admin services to use EventStore command/query pipelines instead of non-existent direct Hexalith.Tenants REST endpoints.

## Story

As a **database administrator using the Hexalith EventStore admin dashboard**,
I want **tenant management (create, enable/disable, user management) to work correctly by routing through EventStore's command and query pipelines**,
so that **I can perform self-service tenant administration through the admin UI, CLI, and MCP without errors**.

## Acceptance Criteria

1. **Commands route through EventStore pipeline** — All tenant write operations (create, disable, enable, add user, remove user, change role) submit commands via HTTP POST to EventStore's `POST /api/v1/commands` endpoint using DAPR service invocation to `EventStoreAppId`. Command envelope uses `Tenant: "system"`, `Domain: "tenants"`, `CommandType: nameof(T)` (e.g., `"CreateTenant"`), `AggregateId: tenantId`, `Payload: JsonSerializer.SerializeToUtf8Bytes(command)`. JWT token forwarded from `IAdminAuthContext`.

2. **Queries route through EventStore pipeline** — All tenant read operations (list, detail, users) submit queries via HTTP POST to EventStore's `POST /api/v1/queries` endpoint using DAPR service invocation to `EventStoreAppId`. Query uses `SubmitQueryRequest` from Contracts with `Tenant: "system"`, `Domain: "tenants"`, `QueryType` from `Hexalith.Tenants.Contracts.Queries` contracts (e.g., `"list-tenants"`, `"get-tenant"`, `"get-tenant-users"`). Response `SubmitQueryResponse.Payload` (JsonElement) deserialized to admin DTOs.

3. **Models aligned with Hexalith.Tenants.Contracts** — `TenantStatusType` = `{Active, Disabled}`. `TenantSummary` = `(TenantId, Name, Status)`. `TenantDetail` = `(TenantId, Name, Description?, Status, CreatedAt)`. `TenantUser` = `(UserId, Role)`. `CreateTenantRequest` = `(TenantId, Name, Description?)`. User request DTOs use `UserId` not `Email`.

4. **Quotas/Compare removed** — `TenantQuotas`, `TenantComparison`, `TenantCompareRequest` types deleted. `GetTenantQuotasAsync`, `CompareTenantUsageAsync` removed from `ITenantQueryService`. Quotas endpoint and compare endpoint removed from `AdminTenantsController`. UI quotas stat card and compare button removed.

5. **Admin UI simplified** — `Tenants.razor` updated: create dialog has 3 fields (TenantId, Name, Description) instead of multi-step wizard. Status filter shows Active/Disabled only. User management uses UserId. No quota progress bars. No compare dialog.

6. **CLI tenant commands updated** — `TenantQuotasCommand` and `TenantCompareCommand` removed or return "not available". `TenantListCommand`, `TenantDetailCommand`, `TenantUsersCommand` updated for new model shapes. `TenantVerifyCommand` updated (no Onboarding status).

7. **MCP tenant tools updated** — `AdminApiClient.Tenants.cs` quotas method removed. `TenantTools.cs` quotas tool removed. Remaining tools updated for new model shapes.

8. **Project compiles with zero warnings** — `dotnet build Hexalith.EventStore.slnx --configuration Release` succeeds.

9. **Existing tests pass** — All Tier 1 tests pass. bUnit tests for Tenants page updated for new model shapes and removed features.

## Tasks / Subtasks

- [x] **Task 1: Add project reference and align models** (AC: 3, 4)
    - [x] 1.1 Add `Hexalith.Tenants.Contracts` project reference to `src/Hexalith.EventStore.Admin.Server/Hexalith.EventStore.Admin.Server.csproj`:
        ```xml
        <ProjectReference Include="../../Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Hexalith.Tenants.Contracts.csproj" />
        ```
    - [x] 1.2 Update `TenantStatusType.cs`: `{Active, Suspended, Onboarding}` -> `{Active, Disabled}`
    - [x] 1.3 Simplify `TenantSummary.cs`: `(TenantId, DisplayName, Status, EventCount, DomainCount)` -> `(TenantId, Name, TenantStatusType Status)`. Remove validation logic on DisplayName, keep TenantId validation.
    - [x] 1.4 Simplify `TenantDetail.cs`: `(TenantId, DisplayName, Status, EventCount, DomainCount, StorageBytes, CreatedAtUtc, Quotas, SubscriptionTier)` -> `(TenantId, Name, string? Description, TenantStatusType Status, DateTimeOffset CreatedAt)`. Remove Quotas/SubscriptionTier.
    - [x] 1.5 Simplify `TenantUser.cs`: `(Email, Role, AddedAtUtc)` -> `(UserId, string Role)`. Remove AddedAtUtc.
    - [x] 1.6 Update `CreateTenantRequest.cs`: `(TenantId, DisplayName, SubscriptionTier, MaxEventsPerDay, MaxStorageBytes)` -> `(TenantId, Name, string? Description)`. Keep `[Required]` on TenantId and Name. Keep `[RegularExpression]` on TenantId.
    - [x] 1.7 Update `AddTenantUserRequest.cs`: `Email` -> `UserId`. Remove `[EmailAddress]` attribute, keep `[Required]`.
    - [x] 1.8 Update `RemoveTenantUserRequest.cs`: `Email` -> `UserId`. Remove `[EmailAddress]`.
    - [x] 1.9 Update `ChangeTenantUserRoleRequest.cs`: `Email` -> `UserId`. Remove `[EmailAddress]`.
    - [x] 1.10 Delete `TenantQuotas.cs`, `TenantComparison.cs` from Admin.Abstractions/Models/Tenants/.
    - [x] 1.11 Delete `TenantCompareRequest.cs` from Admin.Server/Models/.
    - [x] 1.12 Update `ITenantQueryService.cs`: remove `GetTenantQuotasAsync` and `CompareTenantUsageAsync`.
    - [x] 1.13 Update `ITenantCommandService.cs`: rename `email` param -> `userId` on `AddUserToTenantAsync`, `RemoveUserFromTenantAsync`, `ChangeUserRoleAsync`.
    - [x] 1.14 **Checkpoint**: `dotnet build src/Hexalith.EventStore.Admin.Abstractions/` compiles (expect downstream errors in Server/UI/CLI/MCP — those are fixed in Tasks 2-5).

- [x] **Task 2: Rewrite DaprTenantCommandService** (AC: 1)
    - [x] 2.1 Rewrite `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantCommandService.cs`. Replace all `InvokeTenantServicePostAsync` calls with HTTP POST to `api/v1/commands` on `_options.EventStoreAppId` via DAPR service invocation. Build command JSON body:
        ```json
        {
            "messageId": "<Guid>",
            "tenant": "system",
            "domain": "tenants",
            "aggregateId": "<tenantId>",
            "commandType": "CreateTenant",
            "payload": {
                "tenantId": "acme",
                "name": "Acme Corp",
                "description": null
            },
            "correlationId": "<Guid or null>",
            "extensions": null
        }
        ```
        **CRITICAL: All field names must be camelCase** — the host's `CommandsController` binds via System.Text.Json with default camelCase naming policy. PascalCase fields will silently bind as null/default.
        Use `Hexalith.Tenants.Contracts.Identity.TenantIdentity.DefaultTenantId` for tenant, `TenantIdentity.Domain` for domain. Use `nameof(CreateTenant)`, `nameof(DisableTenant)`, etc. for commandType. Serialize the `Hexalith.Tenants.Contracts.Commands.*` record as the payload JsonElement. Forward JWT via `_authContext.GetToken()`. Map HTTP 202 -> `AdminOperationResult(true, correlationId)`, 4xx/5xx -> `AdminOperationResult(false, errorMessage)`. Keep timeout pattern with `CancellationTokenSource.CreateLinkedTokenSource`.
    - [x] 2.2 Command type mapping:
        - `CreateTenantAsync(request)` -> `new CreateTenant(request.TenantId, request.Name, request.Description)`, AggregateId = `request.TenantId`
        - `DisableTenantAsync(tenantId)` -> `new DisableTenant(tenantId)`, AggregateId = `tenantId`
        - `EnableTenantAsync(tenantId)` -> `new EnableTenant(tenantId)`, AggregateId = `tenantId`
        - `AddUserToTenantAsync(tenantId, userId, role)` -> validate `Enum.TryParse<TenantRole>(role, out var tenantRole)`, return `AdminOperationResult(false, ..., "Invalid role")` on failure. On success: `new AddUserToTenant(tenantId, userId, tenantRole)`, AggregateId = `tenantId`
        - `RemoveUserFromTenantAsync(tenantId, userId)` -> `new RemoveUserFromTenant(tenantId, userId)`, AggregateId = `tenantId`
        - `ChangeUserRoleAsync(tenantId, userId, newRole)` -> validate `Enum.TryParse<TenantRole>(newRole, out var tenantRole)`, return `AdminOperationResult(false, ..., "Invalid role")` on failure. On success: `new ChangeUserRole(tenantId, userId, tenantRole)`, AggregateId = `tenantId`
    - [x] 2.3 **Checkpoint**: Service compiles, all 6 methods build valid command envelopes.

- [x] **Task 3: Rewrite DaprTenantQueryService** (AC: 2)
    - [x] 3.1 Rewrite `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs`. Replace all direct Tenants-service calls with HTTP POST to `api/v1/queries` on `_options.EventStoreAppId` via DAPR service invocation. Build query body using `SubmitQueryRequest` from `Hexalith.EventStore.Contracts.Queries` (already referenced):
        ```csharp
        var queryRequest = new SubmitQueryRequest(
            Tenant: "system",
            Domain: ListTenantsQuery.Domain,       // "tenants"
            AggregateId: "index",                   // or tenantId
            QueryType: ListTenantsQuery.QueryType,  // "list-tenants"
            Payload: payloadElement,                // optional JsonElement
            EntityId: entityId);                    // routing hint
        ```
        Serialize as JSON, POST to `api/v1/queries` on EventStoreAppId. Deserialize `SubmitQueryResponse` (Contracts type), then deserialize `response.Payload` (JsonElement) to admin DTO types.
    - [x] 3.2 Query method mapping:
        - `ListTenantsAsync()` -> QueryType: `ListTenantsQuery.QueryType` ("list-tenants"), AggregateId: `"index"`, EntityId: userId from auth context. Deserialize payload as `PaginatedResult<TenantSummary>` (Tenants.Contracts type), unwrap `.Items` from paginated result, map each item to admin `TenantSummary`. Admin interface returns flat `IReadOnlyList<TenantSummary>` — pagination not exposed to admin consumers.
        - `GetTenantDetailAsync(tenantId)` -> QueryType: `GetTenantQuery.QueryType` ("get-tenant"), AggregateId: `tenantId`, EntityId: `tenantId`. Deserialize as Tenants.Contracts `TenantDetail`, map to admin `TenantDetail`.
        - `GetTenantUsersAsync(tenantId)` -> QueryType: `GetTenantUsersQuery.QueryType` ("get-tenant-users"), AggregateId: `tenantId`, EntityId: `tenantId`. Deserialize as `PaginatedResult<TenantMember>`, map to admin `TenantUser` list.
    - [x] 3.3 Remove `GetTenantQuotasAsync` and `CompareTenantUsageAsync` implementations.
    - [x] 3.4 Add mapping helpers: `MapTenantSummary(Contracts.TenantSummary)` -> admin `TenantSummary`, `MapTenantDetail(Contracts.TenantDetail)` -> admin `TenantDetail`, `MapTenantUser(Contracts.TenantMember)` -> admin `TenantUser`. Map `TenantStatus.Disabled` -> `TenantStatusType.Disabled`, `TenantStatus.Active` -> `TenantStatusType.Active`.
    - [x] 3.5 **Checkpoint**: Service compiles, all 3 query methods use EventStore pipeline.

- [x] **Task 4: Update AdminTenantsController and downstream consumers** (AC: 4, 5, 6, 7)
    - [x] 4.1 Update `AdminTenantsController.cs`: remove `GetTenantQuotas` endpoint, remove `CompareTenantUsage` endpoint. Rename `request.Email` -> `request.UserId` in `AddUserToTenant`, `RemoveUserFromTenant`, `ChangeUserRole`. Update param names in `AddUserToTenantAsync` call: `(tenantId, request.UserId, request.Role)`.
    - [x] 4.2 Update `AdminTenantApiClient.cs` (Admin.UI): remove `GetTenantQuotasAsync`, `CompareTenantUsageAsync`. Rename email params to userId in `AddUserToTenantAsync`, `RemoveUserFromTenantAsync`, `ChangeUserRoleAsync`. Update request body construction to use `UserId`.
    - [x] 4.3 Update CLI commands in `src/Hexalith.EventStore.Admin.Cli/Commands/Tenant/`:
        - `TenantCommand.cs`: remove quotas and compare subcommands from registration
        - `TenantListCommand.cs`: update column "Display Name" -> "Name", remove EventCount/DomainCount columns
        - `TenantDetailCommand.cs`: simplify output (Name, Description, Status, CreatedAt only), remove SubscriptionTier/StorageBytes/Quotas sections
        - `TenantUsersCommand.cs`: update column "Email" -> "User ID", remove AddedAtUtc column
        - `TenantVerifyCommand.cs`: remove Onboarding status from verdict logic, update for Disabled status
        - Delete or gut `TenantQuotasCommand.cs` and `TenantCompareCommand.cs`
    - [x] 4.4 Update MCP tools in `src/Hexalith.EventStore.Admin.Mcp/`:
        - `AdminApiClient.Tenants.cs`: remove `GetTenantQuotasAsync` method
        - `TenantTools.cs`: remove `tenant-quotas` tool, update `tenant-detail` and `tenant-users` tool descriptions/output formatting
    - [x] 4.5 **Checkpoint**: `dotnet build Hexalith.EventStore.slnx --configuration Release` compiles with zero errors.

- [x] **Task 5: Update Tenants.razor page** (AC: 5)
    - [x] 5.1 Simplify stat cards: remove "Onboarding" card. Keep Total Tenants, Active (Success), Disabled (Error when > 0). 3 cards instead of 4.
    - [x] 5.2 Simplify filter bar: status options = All, Active, Disabled (remove Onboarding, rename Suspended -> Disabled).
    - [x] 5.3 Simplify grid columns: Tenant ID (monospace), Name, Status badge (green Active, red Disabled). Remove Domains, Events, Quota Usage columns.
    - [x] 5.4 Remove "Compare Tenants" button and compare dialog entirely.
    - [x] 5.5 Simplify "Create Tenant" dialog: single-step with 3 fields (TenantId, Name, Description). Remove multi-step wizard, remove tier/quota/initial-admin steps. On submit: call `CreateTenantAsync` only (no `AddUserToTenantAsync` in wizard). Remove all `_wizard*` state variables.
    - [x] 5.6 Update inline detail panel: show TenantId, Name, Description, Status, CreatedAt. Remove EventCount, DomainCount, StorageBytes, Quotas section. Remove quota progress bars.
    - [x] 5.7 Update user management section: display UserId and Role columns (remove Email, AddedAtUtc). "Add User" dialog: UserId field (required, non-empty string) + Role dropdown with values from `TenantRole` enum: `TenantOwner`, `TenantContributor`, `TenantReader`. "Change Role" dropdown uses same values. Remove email validation.
    - [x] 5.8 Remove all quota progress bar rendering (`FluentProgressBar` for quota usage).
    - [x] 5.9 **Checkpoint**: Page loads, list/filter/create/disable/enable/user-management all work against corrected backend.

- [x] **Task 6: Update tests and verify** (AC: 8, 9)
    - [x] 6.1 Update `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TenantsPageTests.cs`: adjust mock data for new model shapes (`TenantSummary(TenantId, Name, Status)` instead of 5-field version). Remove tests for quotas, compare, onboarding features. Update remaining tests for UserId instead of Email. Keep merge-blocking test structure (stat cards, grid rendering, empty state, error banner, heading, auth gating).
    - [x] 6.2 Run `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/` — all tests pass.
    - [x] 6.3 Run `dotnet build Hexalith.EventStore.slnx --configuration Release` — zero warnings.
    - [x] 6.4 Run all Tier 1 tests: `dotnet test tests/Hexalith.EventStore.Contracts.Tests/ && dotnet test tests/Hexalith.EventStore.Client.Tests/ && dotnet test tests/Hexalith.EventStore.Sample.Tests/ && dotnet test tests/Hexalith.EventStore.Testing.Tests/ && dotnet test tests/Hexalith.EventStore.SignalR.Tests/` — all pass.
    - [x] 6.5 **Checkpoint**: Full build + all tests green.

### Review Findings

- [x] [Review][Patch] Paginated tenant queries stop after the first page [src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs:87]
- [x] [Review][Patch] Admin tenant read endpoints collapse query 404/403 errors into 503/500 [src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs:63]
- [x] [Review][Patch] `TenantRole` parsing rejects valid values with surrounding whitespace [src/Hexalith.EventStore.Admin.Server/Services/DaprTenantCommandService.cs:166]
- [x] [Review]\[Defer] Write endpoints still flatten command failures to HTTP 422 [src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs:136] — deferred, pre-existing
- [x] [Review][Patch] Create-dialog tests no longer verify tenant creation behavior [tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TenantsPageTests.cs:178]
- [x] [Review][Patch] User-management tests don't exercise the `UserId`-based write actions [tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TenantsPageTests.cs:396]
- [x] [Review][Patch] Filter tests don't verify the simplified status/search behavior [tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TenantsPageTests.cs:290]
- [x] [Review][Defer] Detail panel refresh can race after tenant selection changes [src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor:944] — deferred, pre-existing

## Dev Notes

### Sprint Change Proposal Reference

**SCP-2026-04-06** (approved) — Full analysis and 12 change proposals at:
`_bmad-output/planning-artifacts/sprint-change-proposal-2026-04-06.md`

### Root Cause

`DaprTenantCommandService` and `DaprTenantQueryService` called REST endpoints on Hexalith.Tenants that don't exist. Hexalith.Tenants is a domain service — commands go through EventStore's command pipeline, queries through the query pipeline. The admin services bypassed both pipelines with fabricated direct HTTP calls.

### Command Pipeline Pattern

EventStore's command endpoint: `POST /api/v1/commands` accepts `SubmitCommandRequest`:

```csharp
record SubmitCommandRequest(
    string MessageId,     // GUID
    string Tenant,        // "system" for tenant management
    string Domain,        // "tenants"
    string AggregateId,   // managed tenant ID
    string CommandType,   // typeof(T).Name e.g. "CreateTenant"
    JsonElement Payload,  // serialized command record
    string? CorrelationId,
    Dictionary<string, string>? Extensions);
```

**Note:** `SubmitCommandRequest` is defined in `Hexalith.EventStore.Models` (host project, NOT in Contracts). The service must build the JSON body manually since Admin.Server cannot reference the host project. Use anonymous object or manual JSON construction.

### Query Pipeline Pattern

EventStore's query endpoint: `POST /api/v1/queries` accepts `SubmitQueryRequest` (in Contracts — already referenced):

```csharp
record SubmitQueryRequest(
    string Tenant,          // "system"
    string Domain,          // "tenants"
    string AggregateId,     // "index" or tenantId
    string QueryType,       // from IQueryContract e.g. "list-tenants"
    string? ProjectionType, // from IQueryContract
    JsonElement? Payload,   // optional pagination params
    string? EntityId);      // routing hint
```

Response: `SubmitQueryResponse(string CorrelationId, JsonElement Payload)` — deserialize Payload to target type.

### Hexalith.Tenants.Contracts Key Types

**Commands** (namespace `Hexalith.Tenants.Contracts.Commands`):

- `CreateTenant(string TenantId, string Name, string? Description)`
- `DisableTenant(string TenantId)`
- `EnableTenant(string TenantId)`
- `AddUserToTenant(string TenantId, string UserId, TenantRole Role)`
- `RemoveUserFromTenant(string TenantId, string UserId)`
- `ChangeUserRole(string TenantId, string UserId, TenantRole NewRole)`

**Enums** (namespace `Hexalith.Tenants.Contracts.Enums`):

- `TenantRole { TenantOwner, TenantContributor, TenantReader }`
- `TenantStatus { Active, Disabled }`

**Query Contracts** (namespace `Hexalith.Tenants.Contracts.Queries`):

- `ListTenantsQuery` — QueryType: `"list-tenants"`, Domain: `"tenants"`, ProjectionType: `"tenant-index"`
- `GetTenantQuery` — QueryType: `"get-tenant"`, Domain: `"tenants"`, ProjectionType: `"tenants"`
- `GetTenantUsersQuery` — QueryType: `"get-tenant-users"`, Domain: `"tenants"`, ProjectionType: `"tenants"`

**Query Response Types:**

- `TenantSummary(string TenantId, string Name, TenantStatus Status)` — from list
- `TenantDetail(string TenantId, string Name, string? Description, TenantStatus Status, IReadOnlyList<TenantMember> Members, IReadOnlyDictionary<string, string> Configuration, DateTimeOffset CreatedAt)` — from detail
- `TenantMember(string UserId, TenantRole Role)` — embedded in detail or from users query
- `PaginatedResult<T>(IReadOnlyList<T> Items, string? Cursor, bool HasMore)` — wraps list results

**Identity** (namespace `Hexalith.Tenants.Contracts.Identity`):

- `TenantIdentity.DefaultTenantId` = `"system"`
- `TenantIdentity.Domain` = `"tenants"`

### Existing Code Patterns to Follow

- DAPR service invocation: `_daprClient.CreateInvokeMethodRequest()` + `_httpClientFactory.CreateClient().SendAsync()`
- JWT forwarding: `_authContext.GetToken()` -> `request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token)`
- Timeout: `CancellationTokenSource.CreateLinkedTokenSource(ct)` + `cts.CancelAfter(TimeSpan.FromSeconds(_options.ServiceInvocationTimeoutSeconds))`
- Error handling: catch `OperationCanceledException` for timeout, `HttpRequestException` for network, return `AdminOperationResult(false, ...)`
- Admin.Server options: `_options.EventStoreAppId` = `"eventstore"`, `_options.TenantServiceAppId` = `"tenants"`

### JSON Serialization Notes

- **Command body:** Use camelCase field names (System.Text.Json default). Build with anonymous object: `new { messageId, tenant, domain, aggregateId, commandType, payload, correlationId, extensions }`.
- **Query response deserialization:** `SubmitQueryResponse.Payload` is a raw `JsonElement`. When deserializing to Tenants.Contracts types (e.g., `PaginatedResult<TenantSummary>`), use `JsonSerializerOptions { PropertyNameCaseInsensitive = true }` to handle casing differences between EventStore's pipeline output and Hexalith.Tenants' serialization.

### Known Limitation: Manual Command Envelope

`SubmitCommandRequest` is in the host project (`Hexalith.EventStore.Models`), not in Contracts. The command service must build the JSON body manually (anonymous object matching the host's shape). If the host ever changes `SubmitCommandRequest` fields, this breaks silently. Mitigated by: (a) the shape is stable (7 fields, unchanged since Epic 3), (b) integration tests will catch mismatches. Future improvement: extract a shared `CommandSubmissionEnvelope` to Contracts.

### JWT and Tenant Authorization

Admin tenant operations use `Tenant: "system"` in the command/query envelope. The authenticated admin user's JWT must include `"system"` in their tenant claims for the command pipeline's tenant validation (SEC-2) to pass. Verify that Keycloak test users with `Admin` role have the `system` tenant claim. If not, this will need a Keycloak realm configuration update (out of scope for this story — flag to Jerome).

### Critical: What NOT to Do

- Do NOT reference `Hexalith.EventStore.Server` project from Admin.Server
- Do NOT use MediatR directly — go through HTTP endpoints
- Do NOT call Hexalith.Tenants service directly — route through EventStore
- Do NOT keep TenantQuotas, TenantComparison, SubscriptionTier — these don't exist in the source domain
- Do NOT use Email for user identification — Hexalith.Tenants uses UserId (JWT sub claim)

### Project Structure Notes

- Admin.Server.csproj currently references: Admin.Abstractions, EventStore.Contracts
- Adding: Hexalith.Tenants.Contracts (relative path: `../../Hexalith.Tenants/src/Hexalith.Tenants.Contracts/`)
- DI registration in `src/Hexalith.EventStore.Admin.Server/Extensions/ServiceCollectionExtensions.cs`

### Testing Notes

- bUnit tests use `AdminUITestContext` base class
- Mock `AdminTenantApiClient` with `Substitute.For<AdminTenantApiClient>()` passing `IHttpClientFactory` + `NullLogger`
- Culture-sensitive assertions: use invariant formatting (lesson from story 16-1)
- Async rendering: use `WaitForAssertion()` with timeout

### References

- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-04-06.md] — Full SCP with approved proposals
- [Source: Hexalith.Tenants/src/Hexalith.Tenants.Contracts/] — Authoritative command/query/enum types
- [Source: src/Hexalith.EventStore/Controllers/CommandsController.cs] — Command submission endpoint pattern
- [Source: src/Hexalith.EventStore/Controllers/QueriesController.cs] — Query submission endpoint pattern
- [Source: src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs] — Query request type (in Contracts)
- [Source: Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/CommandPipeline/] — Command envelope pattern examples

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Ambiguous type reference between Hexalith.Tenants.Contracts.Queries.TenantDetail and Admin.Abstractions TenantDetail — resolved with using aliases
- AdminOperationResult requires 4 args (not 2) — fixed success path
- JsonContent requires `using System.Net.Http.Json` — added missing import

### Completion Notes List

- All 6 tenant write operations now route through EventStore `POST /api/v1/commands` pipeline using DAPR service invocation
- All 3 tenant read operations now route through EventStore `POST /api/v1/queries` pipeline
- TenantQuotas, TenantComparison, TenantCompareRequest types deleted; all consumers updated
- TenantStatusType simplified: Active/Disabled (removed Suspended/Onboarding)
- All models aligned with Hexalith.Tenants.Contracts: UserId not Email, Name not DisplayName
- Tenants.razor simplified: single-step create dialog, no wizard; TenantRole enum values in dropdowns
- CLI: TenantQuotasCommand and TenantCompareCommand deleted; remaining commands updated for new shapes
- MCP: tenant-quotas tool removed; remaining tools updated
- Build: zero warnings, zero errors
- Tier 1 tests: 745 pass (Contracts 271, Client 313, Sample 62, Testing 67, SignalR 32)
- Admin.Abstractions tests: 404 pass
- TenantsPage bUnit tests: 25 pass

### File List

src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/TenantStatusType.cs (modified)
src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/TenantSummary.cs (modified)
src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/TenantDetail.cs (modified)
src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/TenantUser.cs (modified)
src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/CreateTenantRequest.cs (modified)
src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/AddTenantUserRequest.cs (modified)
src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/RemoveTenantUserRequest.cs (modified)
src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/ChangeTenantUserRoleRequest.cs (modified)
src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/TenantQuotas.cs (deleted)
src/Hexalith.EventStore.Admin.Abstractions/Models/Tenants/TenantComparison.cs (deleted)
src/Hexalith.EventStore.Admin.Abstractions/Services/ITenantQueryService.cs (modified)
src/Hexalith.EventStore.Admin.Abstractions/Services/ITenantCommandService.cs (modified)
src/Hexalith.EventStore.Admin.Server/Hexalith.EventStore.Admin.Server.csproj (modified)
src/Hexalith.EventStore.Admin.Server/Models/TenantCompareRequest.cs (deleted)
src/Hexalith.EventStore.Admin.Server/Services/DaprTenantCommandService.cs (rewritten)
src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs (rewritten)
src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs (modified)
src/Hexalith.EventStore.Admin.UI/Services/AdminTenantApiClient.cs (modified)
src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor (rewritten)
src/Hexalith.EventStore.Admin.UI/Components/StreamFilterBar.razor (modified — DisplayName -> Name)
src/Hexalith.EventStore.Admin.Cli/Commands/Tenant/TenantCommand.cs (modified)
src/Hexalith.EventStore.Admin.Cli/Commands/Tenant/TenantListCommand.cs (modified)
src/Hexalith.EventStore.Admin.Cli/Commands/Tenant/TenantDetailCommand.cs (rewritten)
src/Hexalith.EventStore.Admin.Cli/Commands/Tenant/TenantUsersCommand.cs (modified)
src/Hexalith.EventStore.Admin.Cli/Commands/Tenant/TenantVerifyCommand.cs (rewritten)
src/Hexalith.EventStore.Admin.Cli/Commands/Tenant/TenantQuotasCommand.cs (deleted)
src/Hexalith.EventStore.Admin.Cli/Commands/Tenant/TenantCompareCommand.cs (deleted)
src/Hexalith.EventStore.Admin.Mcp/AdminApiClient.Tenants.cs (modified)
src/Hexalith.EventStore.Admin.Mcp/Tools/TenantTools.cs (modified)
tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Tenants/TenantSummaryTests.cs (modified)
tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Tenants/TenantQuotasTests.cs (deleted)
tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Tenants/TenantComparisonTests.cs (deleted)
tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Common/EnumTests.cs (modified)
tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Common/SerializationRoundTripTests.cs (modified)
tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminTenantsControllerTests.cs (modified)
tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprTenantCommandServiceTests.cs (modified)
tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprTenantQueryServiceTests.cs (modified)
tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Tenant/TenantCompareCommandTests.cs (deleted)
tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Tenant/TenantQuotasCommandTests.cs (deleted)
tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Tenant/TenantDetailCommandTests.cs (modified)
tests/Hexalith.EventStore.Admin.Cli.Tests/Commands/Tenant/TenantVerifyCommandTests.cs (modified)
tests/Hexalith.EventStore.Admin.Mcp.Tests/AdminApiClientTenantTests.cs (modified)
tests/Hexalith.EventStore.Admin.Mcp.Tests/TenantToolsTests.cs (modified)
tests/Hexalith.EventStore.Admin.UI.Tests/Pages/TenantsPageTests.cs (modified)

### Change Log

- 2026-04-06: Story 16-5 rework — rewired tenant admin to EventStore command/query pipelines, aligned models with Hexalith.Tenants.Contracts, removed quotas/compare features
