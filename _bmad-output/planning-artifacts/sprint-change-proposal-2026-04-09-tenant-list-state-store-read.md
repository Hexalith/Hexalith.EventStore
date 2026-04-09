# Sprint Change Proposal: Tenant List — Direct State Store Read

**Date:** 2026-04-09
**Triggered by:** Bug B from tenant creation investigation — tenant list always empty in Admin UI despite successful creation
**Scope:** Minor — Direct implementation fix
**Status:** Implemented and verified

---

## 1. Issue Summary

After fixing the tenant creation deadlock (Bug A), tenants are created successfully but the Admin UI tenant list remains empty. The `DaprTenantQueryService` previously routed all queries through EventStore's `POST /api/v1/queries` endpoint, which returns **404 Not Found** because the v2 query pipeline (FR50-FR64) is not yet implemented.

Additionally, the DAPR state store used `keyPrefix: appid` by default, meaning each service's keys were prefixed with its DAPR appId (`tenants||`, `eventstore||`, `eventstore-admin||`). This prevented the Admin.Server from reading projection data written by the tenants service.

**Evidence from Aspire MCP traces:**
- Admin.Server structured logs: `POST /api/v1/queries` returned 404 on every tenant list call
- Redis inspection: key `tenants||projection:tenant-index:singleton` contains valid tenant data but Admin.Server (appId `eventstore-admin`) couldn't read it due to appId key isolation
- DAPR error: `input key 'tenants||...' can't contain '||'` when attempting cross-appId reads with `keyPrefix: none`

---

## 2. Impact Analysis

### Epic Impact
- **Epic 16 (Admin DBA Ops), Story 16-5 (Tenant Management):** Unblocked — tenant list now displays created tenants.
- **All other epics using DAPR state store:** The `keyPrefix: none` change affects ALL services sharing the state store. Keys are no longer isolated by appId. This is acceptable because the EventStore architecture already uses explicit key namespacing (e.g., `projection:tenant-index:singleton`, `system:tenants:testid:events:1`).

### Artifact Conflicts
- **PRD (FR50-FR64):** The query pipeline is v2 scope. This tactical fix bypasses it by reading projections directly from the state store. When the query pipeline is implemented, `DaprTenantQueryService` should be updated to use it.
- **Architecture (ADR-P4):** The fix aligns with ADR-P4 which states "Admin.Server gets read-only access to the event store state store." Direct state store reads are the intended pattern.
- **Architecture (D1):** The `keyPrefix: none` change means all state store keys must be explicitly namespaced to avoid collisions. The existing key naming convention (`{tenant}:{domain}:{aggId}:events:{seq}`) already guarantees uniqueness.

### Technical Impact
- 2 files modified in `Hexalith.EventStore`
- `keyPrefix: none` added to shared DAPR state store component (Aspire extensions)
- `DaprTenantQueryService` rewritten from EventStore query pipeline client to direct state store reader
- Redis FLUSHALL required after migration (old keys have `appId||` prefix, new keys don't)

---

## 3. Recommended Approach

**Selected Path:** Direct Adjustment

### Change 1: Rewrite `DaprTenantQueryService` to read from DAPR state store

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs`

**OLD:** All 3 query methods (`ListTenantsAsync`, `GetTenantDetailAsync`, `GetTenantUsersAsync`) routed through `SubmitQueryAsync` which called EventStore's `/api/v1/queries` (404).

**NEW:** Read projection data directly from DAPR state store using `DaprClient.GetStateAsync<JsonElement>`:
- `ListTenantsAsync` → read `projection:tenant-index:singleton`
- `GetTenantDetailAsync` → read `projection:tenants:{tenantId}`
- `GetTenantUsersAsync` → read `projection:tenants:{tenantId}` and extract Members

Uses `JsonElement` instead of typed read models to avoid `private set` deserialization issues with System.Text.Json.

**Rationale:** Bypasses the non-existent v2 query pipeline. Reads the same projection data that `TenantProjectionHandler` writes after event persistence. When the v2 query pipeline is implemented, this service can be updated to use it.

### Change 2: Add `keyPrefix: none` to shared state store component

**File:** `src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs`

**OLD:** State store component had no `keyPrefix` metadata (defaults to `appid` prefix).

**NEW:** Added `.WithMetadata("keyPrefix", "none")` to the shared state store component.

**Rationale:** All services sharing the same state store need to read/write the same keys. The default `appid` prefix isolates keys per service, preventing the Admin.Server from reading tenant projection data written by the tenants service. With `keyPrefix: none`, all services share a flat key namespace. Key uniqueness is guaranteed by the existing naming convention.

**Migration note:** After applying this change, existing Redis data with `{appId}||` prefixed keys must be flushed. New keys will be written without the prefix.

---

## 4. Follow-up Work

1. **Disable/Enable tenant and Add User operations** — same timeout behavior as the original Bug A. Commands are sent to EventStore but may time out on actor cold start. Requires investigation.
2. **v2 Query Pipeline (FR50-FR64)** — when implemented, `DaprTenantQueryService` should be updated to use the EventStore query endpoint instead of direct state store reads.
3. **Key collision audit** — verify that no existing state store keys from different services collide after removing the appId prefix.

---

## 5. Implementation Handoff

**Scope Classification:** Minor — Direct implementation by dev team.
**Status:** Implemented and verified via curl API test and Admin UI.

**Verification:**
- `POST /api/v1/admin/tenants` → 202 (tenant created)
- `GET /api/v1/admin/tenants` → `[{"status":0,"tenantId":"finaltest2","name":"Final Test 2"}]`
- Admin UI displays tenant in list with correct name and status
