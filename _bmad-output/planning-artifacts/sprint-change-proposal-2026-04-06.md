# Sprint Change Proposal — Fix Tenant Management Admin Integration

**Date:** 2026-04-06
**Author:** Jerome (via Correct Course workflow)
**Change Scope:** Moderate
**Status:** Approved (2026-04-06)

---

## Section 1: Issue Summary

**Problem Statement:** The EventStore admin tenant management implementation (Story 16-5, marked done) is architecturally broken. `DaprTenantCommandService` and `DaprTenantQueryService` call REST endpoints on the Hexalith.Tenants peer service that do not exist. Hexalith.Tenants is a domain service — commands go through EventStore's command pipeline, queries go through EventStore's query pipeline. The admin services bypass both pipelines with fabricated direct HTTP calls.

**Discovery context:** Attempting to use tenant create/manage features in the admin UI. All write operations fail (non-existent endpoints). Read operations fail (wrong API paths and mismatched response models).

**Evidence:**
- `DaprTenantCommandService.cs` POSTs to `api/v1/tenants`, `api/v1/tenants/{id}/disable`, etc. — Hexalith.Tenants has no command controller
- `DaprTenantQueryService.cs` GETs from `api/v1/tenants` — Hexalith.Tenants serves at `api/tenants` (no v1 prefix) and uses cursor-based pagination
- `TenantQuotas`, `TenantComparison`, `SubscriptionTier` — no backing data source in Hexalith.Tenants domain
- Admin.Abstractions duplicates model types instead of aligning with `Hexalith.Tenants.Contracts`
- Architecture doc (line 220) correctly states "they never bypass the Tenants command pipeline" — implementation violates this

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Impact |
|------|--------|
| Epic 16 (Admin Web UI — DBA Operations) | **Story 16-5 rework** — service implementations, model alignment, UI simplification |
| Epic 17 (Admin CLI) | Minimal — CLI calls AdminTenantsController; controller stays, backend changes are transparent |
| Epic 18 (Admin MCP Server) | Minimal — same pattern as CLI |
| Epics 1-15, 19-20 | No impact |

### Artifact Conflicts

| Artifact | Conflict | Resolution |
|----------|----------|------------|
| PRD | None — FR77 already says "via Hexalith.Tenants" | No change needed |
| Architecture | Code violates stated pattern (line 220) | Fix code to conform; minor clarification to specify command/query pipeline routing |
| UX Design | Quotas/Compare features reference non-existent data | Remove from admin UI; re-add when Hexalith.Tenants grows these capabilities |
| Sprint Status | Story 16-5 marked done but broken | Reopen for rework |

### Technical Impact

- **New dependency:** `Admin.Server.csproj` adds `Hexalith.Tenants.Contracts` project reference
- **No new project references to EventStore.Server** — commands/queries submitted via HTTP to EventStore's own REST endpoints
- **Downstream consumers (CLI, MCP, UI) mostly unaffected** — they consume `AdminTenantsController` which delegates to fixed services

---

## Section 3: Recommended Approach

**Selected path:** Direct Adjustment — rewrite 2 service implementations, align models, simplify UI.

**Rationale:**
1. **Surgical fix** — only service layer changes; UI, controller, CLI, MCP layers need minor adjustments
2. **Preserves working code** — Tenants.razor layout, AdminTenantsController structure, CLI/MCP tenant commands all stay
3. **Conforms to architecture** — commands and queries flow through EventStore's pipelines as designed
4. **Low risk** — the command/query pipelines are battle-tested (Epics 1-11 done)
5. **Scope reduction** — quotas/compare features removed cleanly (no backing data)

**Effort estimate:** Medium — 2 service rewrites + model alignment + UI adjustments
**Risk level:** Low — contained to admin backend, all consuming layers stable
**Timeline impact:** None — corrective fix within completed epic

---

## Section 4: Detailed Change Proposals

### 4.1 Project Reference

**File:** `src/Hexalith.EventStore.Admin.Server/Hexalith.EventStore.Admin.Server.csproj`

**Add:**
```xml
<ProjectReference Include="../../Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Hexalith.Tenants.Contracts.csproj" />
```

**Rationale:** Needed for command type names (`nameof(CreateTenant)`) and query contract metadata (`ListTenantsQuery.QueryType`, `.Domain`).

---

### 4.2 Service Rewrite: DaprTenantCommandService

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantCommandService.cs`

**Was:** DAPR service invocation to non-existent Tenants endpoints
**Becomes:** HTTP POST to EventStore's `api/v1/commands` via DAPR service invocation to `EventStoreAppId`

**Command envelope pattern:**
- `Tenant` = `"system"` (TenantIdentity.DefaultTenantId)
- `Domain` = `"tenants"` (TenantIdentity.Domain)
- `AggregateId` = managed tenant ID
- `CommandType` = `nameof(CreateTenant)`, `nameof(DisableTenant)`, etc.
- `Payload` = serialized command record from `Hexalith.Tenants.Contracts.Commands`
- JWT forwarded via `IAdminAuthContext`
- 202 = `AdminOperationResult(true, correlationId)`, 4xx/5xx = `AdminOperationResult(false, error)`

**Methods unchanged:** `CreateTenantAsync`, `DisableTenantAsync`, `EnableTenantAsync`, `AddUserToTenantAsync`, `RemoveUserFromTenantAsync`, `ChangeUserRoleAsync`

---

### 4.3 Service Rewrite: DaprTenantQueryService

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs`

**Was:** DAPR service invocation to wrong Tenants API paths
**Becomes:** HTTP POST to EventStore's `api/v1/queries` via DAPR service invocation to `EventStoreAppId`

**Query submission pattern (uses `SubmitQueryRequest` from Contracts):**
- `Tenant` = `"system"`
- `Domain` = query contract's `Domain` (= `"tenants"`)
- `QueryType` = query contract's `QueryType`
- `AggregateId` = `"index"` for list, `tenantId` for detail/users
- Response: `SubmitQueryResponse.Payload` deserialized to admin DTOs

**Method mapping:**

| Method | Query Contract | AggregateId | EntityId |
|--------|---------------|-------------|----------|
| `ListTenantsAsync` | `ListTenantsQuery` | `"index"` | userId |
| `GetTenantDetailAsync` | `GetTenantQuery` | tenantId | tenantId |
| `GetTenantUsersAsync` | `GetTenantUsersQuery` | tenantId | tenantId |

**Removed methods:** `GetTenantQuotasAsync`, `CompareTenantUsageAsync`

---

### 4.4 Interface Changes

**File:** `src/Hexalith.EventStore.Admin.Abstractions/Services/ITenantQueryService.cs`
- Remove `GetTenantQuotasAsync`
- Remove `CompareTenantUsageAsync`

**File:** `src/Hexalith.EventStore.Admin.Abstractions/Services/ITenantCommandService.cs`
- Rename `email` parameter → `userId` on `AddUserToTenantAsync`, `RemoveUserFromTenantAsync`, `ChangeUserRoleAsync`

---

### 4.5 Model Alignment

**Align with Hexalith.Tenants.Contracts:**

| File | Change |
|------|--------|
| `TenantStatusType.cs` | `{Active, Suspended, Onboarding}` → `{Active, Disabled}` |
| `CreateTenantRequest.cs` | `(TenantId, DisplayName, SubscriptionTier, MaxEventsPerDay, MaxStorageBytes)` → `(TenantId, Name, Description?)` |
| `AddTenantUserRequest.cs` | `Email` → `UserId` |
| `RemoveTenantUserRequest.cs` | `Email` → `UserId` |
| `ChangeTenantUserRoleRequest.cs` | `Email` → `UserId` |
| `TenantSummary.cs` | `(TenantId, DisplayName, Status, EventCount, DomainCount)` → `(TenantId, Name, Status)` |
| `TenantDetail.cs` | `(TenantId, DisplayName, Status, EventCount, DomainCount, StorageBytes, CreatedAtUtc, Quotas, SubscriptionTier)` → `(TenantId, Name, Description?, Status, CreatedAt)` |
| `TenantUser.cs` | `(Email, Role, AddedAtUtc)` → `(UserId, Role)` |

**Delete:**
| File | Reason |
|------|--------|
| `TenantQuotas.cs` | No backing data source |
| `TenantComparison.cs` | No backing data source |
| `TenantCompareRequest.cs` | No backing endpoint |

---

### 4.6 Controller Changes

**File:** `src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs`
- Remove `GetTenantQuotas` endpoint
- Remove `CompareTenantUsage` endpoint
- Rename `request.Email` → `request.UserId` in user operation endpoints

---

### 4.7 UI Changes

**File:** `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor`
- Remove Compare Tenants button and dialog
- Remove quotas stat card
- Remove Onboarding status filter option
- Simplify Create Tenant dialog (TenantId, Name, Description)
- Update user management to use UserId instead of Email
- Align status display with `{Active, Disabled}`

---

### 4.8 Downstream Consumers (CLI, MCP)

**CLI** (`src/Hexalith.EventStore.Admin.Cli/Commands/Tenant/`):
- `TenantQuotasCommand.cs` — remove or return "not available"
- `TenantCompareCommand.cs` — remove or return "not available"
- `TenantUsersCommand.cs` — display UserId instead of Email

**MCP** (`src/Hexalith.EventStore.Admin.Mcp/`):
- `AdminApiClient.Tenants.cs` — remove quotas/compare methods
- `TenantTools.cs` — remove quotas/compare tools, align user params

---

## Section 5: Implementation Handoff

**Change scope:** Moderate — backend rewrite + model alignment + UI adjustment

**Handoff:** Development team for direct implementation

**Implementation sequence:**
1. Add `Hexalith.Tenants.Contracts` project reference to Admin.Server
2. Rewrite `DaprTenantCommandService` (commands via EventStore pipeline)
3. Rewrite `DaprTenantQueryService` (queries via EventStore pipeline)
4. Align Admin.Abstractions models and interfaces
5. Update `AdminTenantsController` (remove quotas/compare, rename params)
6. Update `Tenants.razor` (remove quotas/compare UI, simplify create form)
7. Update CLI and MCP tenant commands
8. Update/add tests for new service implementations
9. Verify full round-trip: admin UI → controller → service → EventStore pipeline → Hexalith.Tenants domain → response

**Success criteria:**
- Create Tenant from admin UI → command flows through EventStore pipeline → TenantCreated event persisted
- List Tenants from admin UI → query flows through EventStore query pipeline → tenant list displayed
- Enable/Disable tenant from admin UI → state changes reflected
- User management (add/remove/change role) works end-to-end
- CLI `tenant list`, `tenant detail`, `tenant users` return correct data
- MCP tenant tools functional
