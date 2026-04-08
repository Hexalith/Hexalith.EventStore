# Sprint Change Proposal — Tenant Bootstrap Wiring

**Date:** 2026-04-08
**Trigger:** Story 16-5 (Tenant Management) code complete but bootstrap not wired in AppHost
**Scope:** Minor — 2 file edits, configuration only
**Status:** Approved and implemented

## Section 1: Issue Summary

The Tenant Management feature (story 16-5) has all code completed — Admin UI, CLI, MCP, command/query services all rewired to EventStore pipelines. However, two configuration gaps prevent a clean first-run experience:

1. **`BootstrapGlobalAdminUserId` not configured in AppHost** — The `TenantBootstrapHostedService` skips bootstrap on every Aspire launch because the AppHost doesn't pass this env var to the Tenants service. This means the `GlobalAdministratorAggregate` has no records, creating a domain consistency gap.

2. **Keycloak user IDs are random UUIDs** — Users in `hexalith-realm.json` have no `id` field, so Keycloak generates random UUIDs at import. The JWT `sub` claim is unpredictable across container rebuilds, making any bootstrap userId configuration fragile.

**Key finding during analysis:** The Admin UI tenant management **already works end-to-end** without the bootstrap. `CreateTenant` has no RBAC check, and other RBAC checks use JWT claims (via `actor:globalAdmin` extension) rather than the domain-level `GlobalAdministratorAggregate`. The `admin-user` in Keycloak has `global_admin: ["true"]` and `tenants: ["system"]`, which is sufficient for full tenant administration.

The Tenants page shows empty because no tenants have been created yet — not because of a blocking infrastructure issue.

## Section 2: Impact Analysis

### Epic Impact
- **Epic 16** (in-progress): Story 16-5 is the only remaining story. This change closes a flagged gap in the story's JWT notes.
- No other epics affected (all others are `done`).

### Artifact Conflicts
- **PRD:** No conflicts. Multi-tenancy (FR26-FR29) works as designed.
- **Architecture:** Minor gap — no first-run initialization path specified for development.
- **UI/UX:** No changes needed. Tenants page works correctly.

### Technical Impact
- 2 files modified (AppHost `Program.cs`, Keycloak realm JSON)
- Zero domain logic changes
- Zero test changes (bootstrap is idempotent, existing tests unaffected)

## Section 3: Recommended Approach

**Direct Adjustment** — Add configuration to existing files.

**Rationale:**
- The pipeline works; only the bootstrapping wiring is missing
- Config-only change = zero risk to existing functionality
- Bootstrap is idempotent (`GlobalAdminAlreadyBootstrappedRejection` on re-runs)
- Deterministic user IDs improve debuggability across the system

**Effort estimate:** Low (~1 hour)
**Risk level:** Low
**Timeline impact:** None

## Section 4: Detailed Change Proposals

### Proposal 1: Fix Keycloak user IDs (APPROVED)

**File:** `src/Hexalith.EventStore.AppHost/KeycloakRealms/hexalith-realm.json`

Add fixed `id` field to each user:
- `admin-user` → `"id": "admin-user"`
- `tenant-a-user` → `"id": "tenant-a-user"`
- `tenant-b-user` → `"id": "tenant-b-user"`
- `readonly-user` → `"id": "readonly-user"`
- `no-tenant-user` → `"id": "no-tenant-user"`

**Rationale:** Makes JWT `sub` claims deterministic and debuggable. Required for bootstrap userId to match runtime identity.

### Proposal 2: Configure bootstrap in AppHost (APPROVED)

**File:** `src/Hexalith.EventStore.AppHost/Program.cs`

Add after the tenants resource definition (line 69):
```csharp
.WithEnvironment("Tenants__BootstrapGlobalAdminUserId", "admin-user");
```

**Rationale:** Matches the fixed Keycloak user ID. Bootstrap is idempotent — safe for repeated Aspire starts.

### Proposal 3: Tenants appsettings.json default (SKIPPED)

Keep `BootstrapGlobalAdminUserId` empty in appsettings.json. Bootstrap is an orchestrator concern — each AppHost sets its own value.

## Section 5: Implementation Handoff

**Scope classification:** Minor — Direct implementation by dev team

**Changes:** 2 files, configuration only
- `src/Hexalith.EventStore.AppHost/KeycloakRealms/hexalith-realm.json` — add `id` fields
- `src/Hexalith.EventStore.AppHost/Program.cs` — add bootstrap env var

**Success criteria:**
1. Aspire starts → Tenants service logs "Bootstrap command sent for global administrator: UserId=admin-user" (not "Bootstrap skipped")
2. Admin UI Tenants page → "Create Tenant" works → tenant appears in list
3. `dotnet build Hexalith.EventStore.slnx --configuration Release` — zero warnings
4. All existing Tier 1 tests pass

**No sprint-status.yaml update needed** — story 16-5 remains `in-progress` until implementation is verified.

## Addendum: Runtime Bugs Discovered During Verification (2026-04-08)

### Bug Fix 1: Bootstrap crash — HttpContext not available in AuthorizationBehavior

**File:** `src/Hexalith.EventStore/Pipeline/AuthorizationBehavior.cs` (line 34)

**Root cause:** `TenantBootstrapHostedService` sends a `SubmitCommand` via MediatR directly (no HTTP request). The `AuthorizationBehavior` pipeline step required a non-null `HttpContext`, throwing `InvalidOperationException`.

**Fix:** Skip API-level authorization when `HttpContext` is null (internal service calls). Domain-level RBAC in aggregate `Handle` methods still applies as defense-in-depth.

### Bug Fix 2: "Create Tenant" button invisible — AdminUserContext missing global_admin fallback

**File:** `src/Hexalith.EventStore.Admin.UI/Services/AdminUserContext.cs`

**Root cause:** The `<AuthorizedView MinimumRole="AdminRole.Admin">` component checks for `eventstore:admin-role` claim. This claim is added by `AdminClaimsTransformation` on the **Admin Server** only — the Admin UI reads raw Keycloak JWT which doesn't contain it. Result: `AdminUserContext.GetRoleAsync()` always returned `ReadOnly`, hiding all admin buttons.

**Fix:** `AdminUserContext.GetRoleAsync()` now falls back to checking the `global_admin` / `is_global_admin` JWT claims directly when `eventstore:admin-role` is absent.

### Test added: Keycloak realm configuration validation

**File:** `tests/Hexalith.EventStore.IntegrationTests/Configuration/KeycloakRealmConfigurationTests.cs`

3 tests validating Keycloak realm JSON structure:
- `AllUsers_HaveDeterministicIds` — prevents bootstrap userId mismatch
- `AdminUser_HasGlobalAdminAttribute` — prevents RBAC bypass loss
- `AdminUser_HasSystemTenantClaim` — prevents tenant management lockout

### Bug Fix 3: Bootstrap crash on DAPR timeout — OperationCanceledException re-thrown

**File:** `Hexalith.Tenants/src/Hexalith.Tenants/Bootstrap/TenantBootstrapHostedService.cs` (line 49)

**Root cause:** `TaskCanceledException` (from DAPR actor HTTP timeout) inherits from `OperationCanceledException`. The bootstrap catch block `catch (OperationCanceledException) { throw; }` re-threw it, crashing the host. This was intended to propagate host shutdown only.

**Fix:** Added `when (cancellationToken.IsCancellationRequested)` filter so only actual host shutdowns re-throw. DAPR timeouts now fall through to the generic exception handler which logs a warning and lets the service continue.

### Known remaining issue: "Create Tenant" button invisible

The `AuthorizedView` component checks `AdminUserContext.GetRoleAsync()` which relies on `AuthenticationStateProvider`. The Admin UI is a Blazor Server app — the browser connects via WebSocket without a JWT. The `AdminApiAccessTokenProvider` acquires tokens server-side for API calls but doesn't feed them into the Blazor authentication state. Result: anonymous user → `ReadOnly` role → admin buttons hidden. **Requires a custom `AuthenticationStateProvider` — deferred to next commit.**
