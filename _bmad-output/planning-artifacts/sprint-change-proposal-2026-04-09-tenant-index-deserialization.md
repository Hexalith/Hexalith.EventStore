# Sprint Change Proposal — Tenant Admin UI Bugs (Index, Commands, User Display)

**Date:** 2026-04-09
**Triggered by:** Story 16-5 (Tenant Management — Admin Web UI)
**Scope Classification:** Minor
**Status:** Approved & Implemented

---

## Section 1: Issue Summary

### Problem Statement

On the Admin UI Tenants page (`/tenants`), three critical bugs were discovered during tenant management testing:

1. **Bug A (Critical):** When creating a second tenant, the first tenant disappears from the list. Only the most recently created tenant is visible, regardless of the filter selection (All/Active/Disabled).
2. **Bug B (High):** Disable tenant and Add User To Tenant operations fail with 500 Internal Server Error from the Tenants domain service.
3. **Bug C (Medium):** After successfully adding users to a tenant, they do not appear in the detail panel — even after refresh.

### Root Causes

**Bug A — Tenant index lost on each projection update**

`TenantIndexReadModel` properties use `private set`, preventing `System.Text.Json` deserialization when DAPR's `GetStateAsync<TenantIndexReadModel>()` reads from the state store. The dictionaries come back empty, so only the current tenant's events are applied and previous tenants are silently lost.

Same issue affects `TenantState`, `TenantReadModel`, and `GlobalAdministratorReadModel`.

**Bug B — Domain service /process returns 500**

Two distinct failure modes:

1. **Processor mismatch crash:** The `DomainServiceRequestHandler` iterates through ALL registered processors (both `TenantAggregate` and `GlobalAdministratorsAggregate`). When `GlobalAdministratorsAggregate` receives tenant events (e.g., `TenantCreated`) that have no matching `Apply` method on `GlobalAdministratorsState`, the `DomainProcessorStateRehydrator.ResolveApplyMethod()` throws `InvalidOperationException`. This crashes the `/process` endpoint with 500 before the correct processor (`TenantAggregate`) gets a chance to handle the command.

2. **Rejection event poisoning:** After a domain rejection (e.g., `UserAlreadyInTenantRejection`), rejection events are persisted in the aggregate's event stream. On subsequent commands, `ResolveApplyMethod` encounters these rejection event types — which have no `Apply` methods on any state class — and throws, preventing state rehydration for ALL processors.

**Bug C — Users not displayed in detail panel**

`DaprTenantQueryService.GetTenantUsersAsync()` calls `prop.Value.GetString()` on the role value from the `Members` dictionary. `TenantRole` is an enum serialized as an integer (0/1/2) by `System.Text.Json`. `GetString()` throws `InvalidOperationException` on `JsonValueKind.Number`, the exception is caught by the generic handler, and an empty list is returned.

### Evidence

- **Bug A:** Verified with `System.Text.Json` round-trip test: `private set` -> `Tenants.Count = 0`; `public set` -> `Tenants.Count = 2`
- **Bug B:** Dead letter queue in Redis contains `DomainServiceException: 500 Internal Server Error` entries; Aspire structured logs show `"Unable to rehydrate aggregate state 'GlobalAdministratorsState'. Event type 'TenantCreated' has no matching Apply method"`
- **Bug C:** Redis projection data confirms Members dictionary contains users with integer roles (`{"test-user": 0}`), but `GetString()` on integers throws

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Impact | Details |
|------|--------|---------|
| **Epic 16 (Admin Web UI — DBA Ops)** | Story 16-5 blocked | Tenant list, lifecycle, and user management all non-functional |
| Epics 1-15, 17-18 | No impact | Multi-tenant isolation at contract/actor/DAPR level unaffected |

### Story Impact

| Story | Impact |
|-------|--------|
| **16-5 (Tenant Management)** | Directly blocked — all tenant operations broken |

### Artifact Conflicts

| Artifact | Conflict? | Details |
|----------|-----------|---------|
| PRD | None | FR77 requirements unchanged |
| Architecture | Pattern clarification | Models round-tripped through DAPR state store must use `public set`; state rehydrator must tolerate unknown event types |
| UX Design | None | UI code correctly implemented |
| Epics | None | No scope or ordering changes |

### Technical Impact

- **Hexalith.Tenants submodule** — 4 model files changed (`private set` → `set`)
- **Hexalith.EventStore.Client** — `DomainProcessorStateRehydrator` changed to skip unknown events instead of throwing
- **Hexalith.EventStore.Admin.Server** — `DaprTenantQueryService` changed to handle integer enum roles
- **State store data** — existing corrupted data must be cleared (Redis FLUSHALL) after fix deployment

---

## Section 3: Recommended Approach

### Selected Path: Direct Adjustment

All three bugs are targeted fixes with clear root causes. No architectural or scope changes required.

- **Effort:** Low (7 files changed)
- **Risk:** Low (isolated changes, well-understood root causes)
- **Timeline Impact:** None

---

## Section 4: Detailed Change Proposals

### Repo: Hexalith.Tenants (submodule)

#### Change 1: Fix deserialization on all state/projection models

**Files:**
- `src/Hexalith.Tenants.Server/Projections/TenantIndexReadModel.cs`
- `src/Hexalith.Tenants.Server/Aggregates/TenantState.cs`
- `src/Hexalith.Tenants.Server/Projections/TenantReadModel.cs`
- `src/Hexalith.Tenants.Server/Projections/GlobalAdministratorReadModel.cs`

**Change:** All `private set` → `set` on properties that are round-tripped through DAPR state store or JSON deserialization.

**Rationale:** `System.Text.Json` cannot populate `private set` properties during deserialization. This caused the tenant index to lose data, and prevented state rehydration for command processing.

### Repo: Hexalith.EventStore

#### Change 2: Make state rehydrator tolerant of unknown event types

**File:** `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs`

**OLD:**
```csharp
private static MethodInfo ResolveApplyMethod(...) {
    // ... lookup ...
    return applyMethod ?? throw new InvalidOperationException(
        $"Unable to rehydrate aggregate state '{stateType.Name}'. Event type '{eventTypeName}' has no matching Apply method.");
}
```

**NEW:**
```csharp
private static MethodInfo? TryResolveApplyMethod(...) {
    // ... lookup ...
    return applyMethod; // null instead of throw
}
// All 3 callers updated to skip events when applyMethod is null
```

**Rationale:** Event streams contain rejection events and events from other aggregate types that don't have matching `Apply` methods on every state class. Throwing prevents state rehydration entirely. Skipping unknown events is the correct behavior — they don't modify aggregate state.

#### Change 3: Fix tenant user role display

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs`

**OLD:**
```csharp
users.Add(new TenantUser(prop.Name, prop.Value.GetString() ?? "Unknown"));
```

**NEW:**
```csharp
string role = prop.Value.ValueKind == JsonValueKind.String
    ? prop.Value.GetString() ?? "Unknown"
    : prop.Value.ValueKind == JsonValueKind.Number
        ? MapTenantRole(prop.Value.GetInt32())
        : "Unknown";
users.Add(new TenantUser(prop.Name, role));
```

**Rationale:** `TenantRole` enum serializes as integer (0/1/2). `GetString()` throws on numbers, exception is swallowed, empty user list returned.

---

## Section 5: Implementation Handoff

### Scope Classification: Minor

All fixes implemented and deployed locally. Ready for commit and PR.

### Files Changed

**Hexalith.Tenants submodule (4 files):**
- `src/Hexalith.Tenants.Server/Projections/TenantIndexReadModel.cs` — `private set` → `set`
- `src/Hexalith.Tenants.Server/Aggregates/TenantState.cs` — `private set` → `set`
- `src/Hexalith.Tenants.Server/Projections/TenantReadModel.cs` — `private set` → `set`
- `src/Hexalith.Tenants.Server/Projections/GlobalAdministratorReadModel.cs` — `private set` → `set`

**Hexalith.EventStore repo (2 files):**
- `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs` — skip unknown events
- `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs` — handle integer enum roles

### Post-deployment

- Redis FLUSHALL required (corrupted projection data from Bug A)
- Verify: create 2+ tenants, disable/enable, add users with all 3 roles

### Success Criteria

- [x] Two or more tenants persist in the list across all filters
- [x] Disable/Enable operations change tenant status
- [x] Add User operation succeeds for all roles (Owner, Contributor, Reader)
- [x] Users appear in the tenant detail panel with correct role labels
- [ ] All Tier 1 tests pass after changes
