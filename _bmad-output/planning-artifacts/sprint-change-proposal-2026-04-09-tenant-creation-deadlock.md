# Sprint Change Proposal: Tenant Creation Deadlock Fix

**Date:** 2026-04-09
**Triggered by:** Admin UI "Create Tenant" button silently fails -- grays out, then resets after 5 seconds with no feedback
**Scope:** Minor -- Direct implementation fix
**Status:** Bug A fixed, Bug B documented for follow-up

---

## 1. Issue Summary

When a user clicks "Create Tenant" in the Admin UI (`/tenants`), the button enters a loading state but the operation silently fails. The dialog remains open and resets after ~5 seconds. No error toast is displayed.

**Root cause (Bug A -- FIXED):** The tenants domain service (`Hexalith.Tenants`) registered the full EventStore server pipeline via `AddEventStoreServer()`, which registered `AggregateActor` as a DAPR actor type on the tenants service. DAPR actor placement distributed some aggregate actors to the tenants service. When these actors tried to invoke the domain service's `/process` endpoint on the same host, it caused:
1. A deadlock due to DAPR actor turn-based concurrency (actor invokes itself)
2. The invocation used AppId `hexalith-tenants-commandapi` from `appsettings.json` instead of the actual DAPR AppId `tenants`, causing 500 errors

**Secondary issue (Bug B -- DOCUMENTED):** Even after tenant creation succeeds, the tenant list remains empty. The `DaprTenantQueryService` queries EventStore's `/api/v1/queries` endpoint which returns 404 -- the query pipeline is not yet implemented on the EventStore.

**Evidence from Aspire MCP traces:**
- Admin UI POST to `/api/v1/admin/tenants` times out at 5s (`TaskCanceledException`)
- Admin.Server forwards to EventStore which accepts the command (202)
- EventStore's `AggregateActor` invokes `tenants/process` -- 10s timeout, retry, 10s timeout, 30s total timeout
- Tenants service console logs show `AggregateActor` calling `hexalith-tenants-commandapi/process` -> 500

---

## 2. Impact Analysis

### Epic Impact
- **Epic 16 (Admin DBA Ops), Story 16-5 (Tenant Management):** Blocked by Bug A (now fixed). Bug B (empty list) is a known limitation pending query pipeline implementation.

### Artifact Conflicts
- **PRD:** No conflict. Tenant management is defined as v2 admin tooling.
- **Architecture:** No conflict. ADR-P4 pattern preserved. The fix corrects a violation where the tenants service incorrectly hosted server-side actors.
- **Keycloak/Auth:** No impact.

### Technical Impact
- 3 files modified in `Hexalith.Tenants` submodule
- 0 new dependencies
- Architecture clarification: domain services must NOT register `AddEventStoreServer`

---

## 3. Recommended Approach

**Selected Path:** Direct Adjustment

**Changes applied:**

### Change 1: Remove `AddEventStoreServer` from tenants `Program.cs`
Removed server-side pipeline registration (`AddEventStoreServer`, `EventStoreWebExtensions.AddEventStore`, `UseEventStore`, `CorrelationIdMiddleware`, `UseRateLimiter`, `UseAuthentication/Authorization`). Added `AddProblemDetails()` and `AddControllers()` for standalone domain service operation.

**Rationale:** Domain services must only host the `/process` endpoint and domain processors. `AggregateActor` must only run on the EventStore.

### Change 2: Rewrite `TenantBootstrapHostedService` to use DAPR HTTP
Replaced MediatR-based `SubmitCommand` (which required the server pipeline) with direct DAPR HTTP invocation to the EventStore's command endpoint.

**Rationale:** The bootstrap service sends commands to the EventStore, not through a local pipeline.

### Change 3: Fix `appsettings.json` AppId
Changed `hexalith-tenants-commandapi` to `tenants` to match the DAPR sidecar AppId.

**Rationale:** The static domain service registration must match the actual DAPR AppId.

---

## 4. Follow-up Work (Bug B)

The tenant list remains empty because:
- `DaprTenantQueryService.ListTenantsAsync()` sends queries to EventStore `/api/v1/queries`
- EventStore returns 404 (query endpoint not implemented)
- `DaprTenantQueryService` handles 404 gracefully by returning empty list

**Options for follow-up:**
1. Implement the EventStore query endpoint (`/api/v1/queries`) -- full query pipeline (FR50-FR64)
2. Have `DaprTenantQueryService` query the tenants service directly via DAPR instead of going through EventStore

This should be tracked as a separate story.

---

## 5. Implementation Handoff

**Scope Classification:** Minor -- Direct implementation by dev team.
**Status:** Bug A fix applied and verified (tenant creation dialog closes on success).

**Verification:**
- Tenant creation succeeds (dialog closes, events persisted, events published)
- Bootstrap service starts without crash
- No `AggregateActor` registered on tenants DAPR sidecar
- Projection update fails with expected `KeyNotFoundException` (known -- pending projection actor setup)

**Remaining tasks:**
- [x] Fix Bug A (tenant creation deadlock)
- [ ] Commit changes
- [ ] Bug B follow-up story (tenant list query pipeline)
